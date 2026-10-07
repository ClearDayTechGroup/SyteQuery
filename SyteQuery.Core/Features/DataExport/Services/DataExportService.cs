using Newtonsoft.Json;
using System.Globalization;
using System.Text;

namespace SyteQuery.Features.DataExport.Services;

public interface IDataExportService
{
    byte[] ExportToCsv(List<Dictionary<string, object?>> rows, CsvExportOptions? options = null);
    byte[] ExportToJson(List<Dictionary<string, object?>> rows, JsonExportOptions? options = null);
}

public class DataExportService : IDataExportService
{
    public byte[] ExportToCsv(List<Dictionary<string, object?>> rows, CsvExportOptions? options = null)
    {
        options ??= new CsvExportOptions();

        if (rows == null || rows.Count == 0)
        {
            return Encoding.UTF8.GetBytes("No data to export");
        }

        var sb = new StringBuilder();
        var columns = rows[0].Keys.ToList();

        // Write headers if enabled
        if (options.IncludeHeaders)
        {
            var headers = columns.Select(c => EscapeCsvValue(c, options.Delimiter));
            sb.AppendLine(string.Join(options.Delimiter, headers));
        }

        // Write data rows
        foreach (var row in rows)
        {
            var values = columns.Select(col =>
            {
                if (row.TryGetValue(col, out var value) && value != null)
                {
                    var stringValue = FormatValue(value);
                    return EscapeCsvValue(stringValue, options.Delimiter);
                }
                return string.Empty;
            });

            sb.AppendLine(string.Join(options.Delimiter, values));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] ExportToJson(List<Dictionary<string, object?>> rows, JsonExportOptions? options = null)
    {
        options ??= new JsonExportOptions();

        if (rows == null || rows.Count == 0)
        {
            return Encoding.UTF8.GetBytes("[]");
        }

        var formatting = options.Indented ? Formatting.Indented : Formatting.None;
        var json = JsonConvert.SerializeObject(rows, formatting);

        return Encoding.UTF8.GetBytes(json);
    }

    private static string FormatValue(object? value)
    {
        if (value == null)
            return string.Empty;

        // Handle JSON.NET types
        if (value is Newtonsoft.Json.Linq.JValue jv)
            return Convert.ToString(jv.Value, CultureInfo.InvariantCulture) ?? string.Empty;

        if (value is Newtonsoft.Json.Linq.JToken jt)
            return jt.ToString(Newtonsoft.Json.Formatting.None);

        // Handle DateTime formatting
        if (value is DateTime dt)
            return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string EscapeCsvValue(string value, string delimiter)
    {
        // Escape if value contains delimiter, quotes, or newlines
        if (value.Contains(delimiter) || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            // Escape quotes by doubling them
            value = value.Replace("\"", "\"\"");
            // Wrap in quotes
            return $"\"{value}\"";
        }

        return value;
    }
}

public class CsvExportOptions
{
    public string Delimiter { get; set; } = ",";
    public bool IncludeHeaders { get; set; } = true;
}

public class JsonExportOptions
{
    public bool Indented { get; set; } = true;
}
