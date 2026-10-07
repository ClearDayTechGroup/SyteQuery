using System.Text;
using System.Text.Json;

namespace SyteQuery.Features.Environments.Services;

/// <summary>
/// Low-level client for the Infor Mongoose REST v2 API (IDORequestService).
/// Replaces the proprietary Mongoose .NET client library (IDOBase/IDOCore/IDOProtocol/etc.)
/// with plain HTTP + JSON calls against the officially documented REST v2 endpoints
/// ("Integrating IDOs with External Applications", Infor Doc 64487, Chapter 3).
/// </summary>
public interface IIdoHttpClient
{
    /// <summary>
    /// Acquires a Mongoose security token for the given environment/config/user.
    /// Per Infor's documentation the token does not expire until the password changes
    /// or the config/user is removed, so callers should cache and reuse it.
    /// </summary>
    Task<IdoTokenResult> GetTokenAsync(string environmentUrl, string config, string user, string password, CancellationToken ct = default);

    /// <summary>
    /// Invokes an IDO method (REST v2 "InvokeIDOMethod": POST /invoke/{ido}?method={method}).
    /// </summary>
    Task<IdoInvokeResult> InvokeAsync(string environmentUrl, string token, string idoName, string methodName, IReadOnlyList<string?> parameters, CancellationToken ct = default);

    /// <summary>
    /// Loads records from an IDO collection (REST v2 "LoadCollection": GET /load/{ido}).
    /// </summary>
    Task<IdoLoadResult> LoadCollectionAsync(string environmentUrl, string token, string idoName, string? properties = null, string? filter = null, int? recordCap = null, CancellationToken ct = default);
}

public sealed record IdoTokenResult(bool Success, string? Token, string? Message);

public sealed record IdoInvokeResult(bool Success, IReadOnlyList<string?> Parameters, string? Message);

public sealed record IdoLoadResult(bool Success, IReadOnlyList<IReadOnlyDictionary<string, string?>> Items, string? Message)
{
    public int ItemCount => Items.Count;
}

public sealed class IdoHttpClient : IIdoHttpClient
{
    private readonly HttpClient _http;
    private readonly ILogger<IdoHttpClient> _logger;

    public IdoHttpClient(HttpClient http, ILogger<IdoHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// The app's stored "environment URL" is the legacy Mongoose/SOAP endpoint
    /// (e.g. https://csi10X.erpsl.inforcloudsuite.com/IDORequestService/RequestService.aspx).
    /// The REST v2 API lives on the same host, under /IDORequestService/ido - so we rebuild
    /// from the host/authority rather than requiring users to re-enter a second URL.
    /// </summary>
    private static string BuildRestBaseUrl(string environmentUrl)
    {
        var uri = new Uri(environmentUrl);
        return $"{uri.GetLeftPart(UriPartial.Authority)}/IDORequestService/ido";
    }

    private static string Enc(string value) => Uri.EscapeDataString(value);

    public async Task<IdoTokenResult> GetTokenAsync(string environmentUrl, string config, string user, string password, CancellationToken ct = default)
    {
        var url = $"{BuildRestBaseUrl(environmentUrl)}/token/{Enc(config)}/{Enc(user)}/{Enc(password)}";

        using var response = await _http.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GetSecurityToken returned HTTP {Status} for config {Config}", (int)response.StatusCode, config);
            return new IdoTokenResult(false, null, $"HTTP {(int)response.StatusCode} requesting security token");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var success = root.TryGetProperty("Success", out var s) && s.ValueKind == JsonValueKind.True;
        var message = root.TryGetProperty("Message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        var token = success && root.TryGetProperty("Token", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;

        return new IdoTokenResult(success, token, message);
    }

    public async Task<IdoInvokeResult> InvokeAsync(string environmentUrl, string token, string idoName, string methodName, IReadOnlyList<string?> parameters, CancellationToken ct = default)
    {
        var url = $"{BuildRestBaseUrl(environmentUrl)}/invoke/{Enc(idoName)}?method={Enc(methodName)}";

        var parmsArray = new System.Text.Json.Nodes.JsonArray();
        foreach (var parm in parameters)
            parmsArray.Add(parm);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(parmsArray.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authorization", token);

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Invoke {Ido}.{Method} returned HTTP {Status}", idoName, methodName, (int)response.StatusCode);
            return new IdoInvokeResult(false, Array.Empty<string?>(), $"HTTP {(int)response.StatusCode} invoking {idoName}.{methodName}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var success = root.TryGetProperty("Success", out var s) && s.ValueKind == JsonValueKind.True;
        var message = root.TryGetProperty("Message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;

        var outParams = new List<string?>();
        if (root.TryGetProperty("Parameters", out var p) && p.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in p.EnumerateArray())
            {
                outParams.Add(element.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => element.GetString(),
                    _ => element.GetRawText()
                });
            }
        }

        return new IdoInvokeResult(success, outParams, message);
    }

    public async Task<IdoLoadResult> LoadCollectionAsync(string environmentUrl, string token, string idoName, string? properties = null, string? filter = null, int? recordCap = null, CancellationToken ct = default)
    {
        var queryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(properties)) queryParts.Add($"properties={Enc(properties)}");
        if (!string.IsNullOrWhiteSpace(filter)) queryParts.Add($"filter={Enc(filter)}");
        if (recordCap.HasValue) queryParts.Add($"recordcap={recordCap.Value}");

        var url = $"{BuildRestBaseUrl(environmentUrl)}/load/{Enc(idoName)}"
            + (queryParts.Count > 0 ? "?" + string.Join("&", queryParts) : string.Empty);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", token);

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LoadCollection {Ido} returned HTTP {Status}", idoName, (int)response.StatusCode);
            return new IdoLoadResult(false, Array.Empty<IReadOnlyDictionary<string, string?>>(), $"HTTP {(int)response.StatusCode} loading {idoName}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var success = root.TryGetProperty("Success", out var s) && s.ValueKind == JsonValueKind.True;
        var message = root.TryGetProperty("Message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;

        var items = new List<IReadOnlyDictionary<string, string?>>();
        if (root.TryGetProperty("Items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsEl.EnumerateArray())
            {
                var dict = new Dictionary<string, string?>();
                foreach (var prop in item.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.Null => null,
                        JsonValueKind.String => prop.Value.GetString(),
                        _ => prop.Value.GetRawText()
                    };
                }
                items.Add(dict);
            }
        }

        return new IdoLoadResult(success, items, message);
    }
}
