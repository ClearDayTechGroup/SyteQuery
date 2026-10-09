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
    Task<IdoTokenResult> GetTokenAsync(string environmentUrl, string config, string user, string password, IdoTokenMode mode = IdoTokenMode.Auto, CancellationToken ct = default);

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

/// <summary>
/// The two forms of Infor's "get a security token" call (REST v2): <c>GET /token/{config}/{username}/{password}</c>
/// and <c>GET /token/{config}</c> with <c>username</c> and <c>password</c> headers.
/// </summary>
public enum IdoTokenStyle
{
    CredentialsInUrl,
    CredentialsInHeaders
}

/// <summary>
/// How an environment asks for its security token. Stored per environment, so the numbers are fixed:
/// they are what is written to the database.
/// </summary>
public enum IdoTokenMode
{
    /// <summary>Try whichever form is likely to work and fall back to the other (see IdoHttpClient.GetTokenAsync).</summary>
    Auto = 0,

    /// <summary>Only ever send credentials in the URL path. One request, no fallback.</summary>
    CredentialsInUrl = 1,

    /// <summary>Only ever send credentials in HTTP headers. One request, no fallback.</summary>
    CredentialsInHeaders = 2
}

/// <summary>
/// Remembers, for this run of the app, which token style worked for each environment/user, so the
/// one that fails isn't tried first every time. Holds no credentials - the key is host, config and user.
/// </summary>
public sealed class IdoTokenStyleMemory
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IdoTokenStyle> _worked = new();

    public IdoTokenStyle? Get(string key) => _worked.TryGetValue(key, out var style) ? style : null;

    public void Remember(string key, IdoTokenStyle style) => _worked[key] = style;
}

public sealed class IdoHttpClient : IIdoHttpClient
{
    private readonly HttpClient _http;
    private readonly ILogger<IdoHttpClient> _logger;
    private readonly IdoTokenStyleMemory _tokenStyles;

    public IdoHttpClient(HttpClient http, ILogger<IdoHttpClient> logger, IdoTokenStyleMemory tokenStyles)
    {
        _http = http;
        _logger = logger;
        _tokenStyles = tokenStyles;
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

    /// <summary>
    /// Gets a security token, trying whichever of Infor's two token calls is likely to work and falling back
    /// to the other if the server doesn't answer the first properly.
    ///
    /// The URL form can't carry some characters in a password or user name (<c>/ \ ? # %</c>, as in
    /// <c>DOMAIN\user</c>), and some gateways refuse credentials in a URL. The header form isn't understood by
    /// every SyteLine version or gateway, and non-ASCII values don't travel well in headers. Neither works
    /// everywhere, so both are supported.
    ///
    /// Falls back only when the server did not give a proper Mongoose answer (an HTTP error, or a reply that
    /// isn't Mongoose JSON). A Mongoose "Success: false" - wrong password - ends it at once: a second attempt
    /// would count as another failed login and could lock the account.
    /// </summary>
    public async Task<IdoTokenResult> GetTokenAsync(string environmentUrl, string config, string user, string password, IdoTokenMode mode = IdoTokenMode.Auto, CancellationToken ct = default)
    {
        var baseUrl = BuildRestBaseUrl(environmentUrl);

        // The environment is set to one form: do exactly that, once. No fallback and nothing remembered, so
        // there is never a second request that could count as another failed login.
        if (mode != IdoTokenMode.Auto)
            return await GetTokenInOneFormAsync(baseUrl, mode == IdoTokenMode.CredentialsInUrl ? IdoTokenStyle.CredentialsInUrl : IdoTokenStyle.CredentialsInHeaders, config, user, password, ct);

        var key = $"{baseUrl}|{config}|{user}";
        var problems = new List<string>();

        foreach (var style in StylesToTry(_tokenStyles.Get(key), config, user, password))
        {
            if (style == IdoTokenStyle.CredentialsInHeaders && !(IsHeaderSafe(user) && IsHeaderSafe(password) && IsHeaderSafe(config)))
            {
                problems.Add($"{Describe(style)}: skipped, the credentials contain characters that can't be sent in an HTTP header");
                continue;
            }

            var attempt = await RequestTokenAsync(baseUrl, style, config, user, password, ct);
            if (attempt.Answer is { } answer)
            {
                if (answer.Success)
                    _tokenStyles.Remember(key, style);
                return answer;
            }

            problems.Add($"{Describe(style)}: {attempt.Problem}");
        }

        return new IdoTokenResult(false, null,
            $"SyteLine didn't answer the security-token request ({string.Join("; ", problems)}). " +
            "Check the URL and the configuration name. If the server sits behind a gateway or proxy, it may be blocking one form of the request.");
    }

    private async Task<IdoTokenResult> GetTokenInOneFormAsync(string baseUrl, IdoTokenStyle style, string config, string user, string password, CancellationToken ct)
    {
        const string Advice = "Change \"Sign-in\" under Tools > Environments > Edit to Automatic to let SyteQuery try the other form too.";

        if (style == IdoTokenStyle.CredentialsInHeaders && !(IsHeaderSafe(user) && IsHeaderSafe(password) && IsHeaderSafe(config)))
        {
            return new IdoTokenResult(false, null,
                "This environment is set to send credentials in HTTP headers, but they contain characters (such as accented letters) that a header can't carry reliably. " + Advice);
        }

        var attempt = await RequestTokenAsync(baseUrl, style, config, user, password, ct);
        if (attempt.Answer is { } answer)
            return answer;

        return new IdoTokenResult(false, null,
            $"SyteLine didn't answer the security-token request ({Describe(style)}: {attempt.Problem}). " +
            $"This environment is set to send credentials only that way. {Advice}");
    }

    /// <summary>The order to try the two styles in.</summary>
    internal static IReadOnlyList<IdoTokenStyle> StylesToTry(IdoTokenStyle? remembered, string config, string user, string password)
    {
        IdoTokenStyle first;
        if (remembered is { } known)
            first = known;
        else if (IsUnsafeInUrl(config) || IsUnsafeInUrl(user) || IsUnsafeInUrl(password))
            first = IdoTokenStyle.CredentialsInHeaders;   // the URL form would mangle it
        else
            first = IdoTokenStyle.CredentialsInUrl;       // what the app has always done

        var second = first == IdoTokenStyle.CredentialsInUrl ? IdoTokenStyle.CredentialsInHeaders : IdoTokenStyle.CredentialsInUrl;
        return new[] { first, second };
    }

    /// <summary>True when the value can't safely be put in a URL path segment: IIS and most gateways treat an encoded slash, backslash, '?', '#' or '%' as something else, and "." / ".." are path navigation.</summary>
    internal static bool IsUnsafeInUrl(string value) =>
        value.IndexOfAny(new[] { '/', '\\', '?', '#', '%' }) >= 0 || value is "." or "..";

    /// <summary>Printable ASCII only - what an HTTP header value can reliably carry.</summary>
    internal static bool IsHeaderSafe(string value) => value.All(c => c >= ' ' && c <= '~');

    private static string Describe(IdoTokenStyle style) =>
        style == IdoTokenStyle.CredentialsInUrl ? "credentials in the URL" : "credentials in headers";

    /// <summary>What one token request came to: a Mongoose answer (success or not), or a description of why there wasn't one.</summary>
    private sealed record TokenAttempt(IdoTokenResult? Answer, string? Problem);

    private async Task<TokenAttempt> RequestTokenAsync(string baseUrl, IdoTokenStyle style, string config, string user, string password, CancellationToken ct)
    {
        using var request = style == IdoTokenStyle.CredentialsInUrl
            ? new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/token/{Enc(config)}/{Enc(user)}/{Enc(password)}")
            : new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/token/{Enc(config)}");

        if (style == IdoTokenStyle.CredentialsInHeaders)
        {
            request.Headers.TryAddWithoutValidation("username", user);
            request.Headers.TryAddWithoutValidation("password", password);
        }

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        // Never log the request: in the URL form it contains the password.
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GetSecurityToken ({Style}) returned HTTP {Status} for config {Config}", style, (int)response.StatusCode, config);
            return new TokenAttempt(null, $"HTTP {(int)response.StatusCode}");
        }

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(body);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            _logger.LogWarning("GetSecurityToken ({Style}) returned a reply that isn't JSON for config {Config}", style, config);
            return new TokenAttempt(null, "a reply that wasn't JSON (a gateway or login page in the way?)");
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Success", out var s) || s.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return new TokenAttempt(null, "a reply that isn't a Mongoose security-token answer");

        var success = s.ValueKind == JsonValueKind.True;
        var message = root.TryGetProperty("Message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        var token = success && root.TryGetProperty("Token", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;

        return new TokenAttempt(new IdoTokenResult(success, token, message), null);
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
