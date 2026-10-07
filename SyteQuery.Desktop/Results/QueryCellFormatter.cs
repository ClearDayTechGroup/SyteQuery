using System.Globalization;

namespace SyteQuery.Desktop.Results;

/// <summary>
/// Same value-unwrapping rules the old Blazor ResultsGrid/export services used - query rows
/// come back from IdoQueryService as Newtonsoft.Json-deserialized Dictionary&lt;string,
/// object?&gt;, so values can show up as JValue/JToken instead of plain CLR types depending
/// on the original JSON shape. Kept as one shared helper so the grid's cell display and its
/// quick-search filter can't drift out of sync with each other.
/// </summary>
public static class QueryCellFormatter
{
    public static string Format(object? value)
    {
        if (value is null)
            return "";

        if (value is Newtonsoft.Json.Linq.JValue jv)
            return Convert.ToString(jv.Value, CultureInfo.InvariantCulture) ?? "";

        if (value is Newtonsoft.Json.Linq.JToken jt)
            return jt.ToString(Newtonsoft.Json.Formatting.None);

        if (value is DateTime dt)
            return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }
}
