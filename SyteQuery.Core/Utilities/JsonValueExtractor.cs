namespace SyteQuery.Utilities;

/// <summary>
/// Utilities for safely extracting string values from JSON.NET types
/// </summary>
public static class JsonValueExtractor
{
    /// <summary>
    /// Safely extracts a string value from various JSON.NET object types
    /// </summary>
    public static string GetStringValue(object? value)
    {
        if (value is null)
            return string.Empty;

        if (value is Newtonsoft.Json.Linq.JValue jv)
            return jv.Value?.ToString() ?? string.Empty;

        if (value is Newtonsoft.Json.Linq.JToken jt)
            return jt.ToString(Newtonsoft.Json.Formatting.None);

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Gets a string value from a dictionary by key
    /// </summary>
    public static string GetStringValue(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var val) || val is null)
            return string.Empty;

        return GetStringValue(val);
    }
}
