using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SyteQuery.Features.DataExport.Services;
using SyteQuery.Features.QueryEditor.Models;

namespace SyteQuery.Features.DatabaseQuery.Services;

/// <summary>What the query IDO sent back, parsed.</summary>
/// <param name="Version">1 = the original IDO (one bare list of rows), 2 = multiple result sets.</param>
/// <param name="ResultSets">Every result set, in order.</param>
/// <param name="Error">Set when the command failed part-way; <paramref name="ResultSets"/> then holds what was read before.</param>
public sealed record IdoPayload(int Version, IReadOnlyList<QueryResultSet> ResultSets, string? Error);

/// <summary>
/// Reads the JSON the query IDO returns (see SyteQuery.IDO/README.md). The one place that knows the formats:
/// <list type="bullet">
/// <item>Version 1, the original IDO: a bare JSON array of row objects - a single result set.</item>
/// <item>Version 2: <c>{"version":2,"resultSets":[{"columns":[...],"rows":[...]}], "error":"..."}</c>.</item>
/// </list>
/// Both may arrive base64-encoded. Reading the old format is deliberate - an environment's IDO is
/// updated by a SyteLine admin, who may not have done it yet when the app is updated.
/// </summary>
public static class IdoResultParser
{
    /// <summary>The newest output format this build of SyteQuery understands.</summary>
    public const int LatestVersion = 2;

    public static bool TryParse(string? payload, out IdoPayload? result, out string? problem)
    {
        result = null;
        problem = null;

        if (string.IsNullOrWhiteSpace(payload))
        {
            problem = "The query returned no data.";
            return false;
        }

        var json = DataEncoder.TryDecodeBase64ToUtf8(payload) ?? payload;

        try
        {
            switch (JToken.Parse(json))
            {
                case JArray legacyRows:
                    result = new IdoPayload(1, new[] { ReadSet(columns: null, legacyRows) }, null);
                    return true;

                case JObject obj when obj["resultSets"] is JArray sets:
                    var version = obj["version"]?.Type == JTokenType.Integer ? (int)obj["version"]! : LatestVersion;
                    if (version > LatestVersion)
                    {
                        problem = $"The IDO returned results in format version {version}, but this version of SyteQuery " +
                                  $"understands up to version {LatestVersion}. Update SyteQuery.";
                        return false;
                    }

                    var parsed = new List<QueryResultSet>(sets.Count);
                    foreach (var set in sets)
                    {
                        var columns = (set["columns"] as JArray)?.Select(c => c.ToString()).ToList();
                        parsed.Add(ReadSet(columns, set["rows"] as JArray ?? new JArray()));
                    }

                    var error = obj["error"]?.Type == JTokenType.String ? (string?)obj["error"] : null;
                    result = new IdoPayload(version, parsed, string.IsNullOrWhiteSpace(error) ? null : error);
                    return true;

                default:
                    problem = $"The IDO returned data in a format SyteQuery doesn't recognise: {Preview(json)}";
                    return false;
            }
        }
        catch (JsonException ex)
        {
            problem = $"Failed to parse query results: {ex.Message}";
            return false;
        }
    }

    private static QueryResultSet ReadSet(IReadOnlyList<string>? columns, JArray rows)
    {
        var list = rows.ToObject<List<Dictionary<string, object?>>>() ?? new List<Dictionary<string, object?>>();
        return new QueryResultSet
        {
            // The original IDO sent no column list, so take the names from the first row.
            Columns = columns ?? (list.Count > 0 ? list[0].Keys.ToList() : new List<string>()),
            Rows = list
        };
    }

    private static string Preview(string json) => json.Length > 120 ? json[..120] + "..." : json;
}
