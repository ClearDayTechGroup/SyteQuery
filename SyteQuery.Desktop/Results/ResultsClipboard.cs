using System.Text;

namespace SyteQuery.Desktop.Results;

/// <summary>
/// Turns a block of result cells into text for the clipboard: tab between cells, newline between rows - what Excel
/// and most editors paste as a grid. A value that itself contains a tab, a newline or a double quote is wrapped in
/// double quotes (quotes doubled), the same convention Excel uses, so one awkward value can't shift everything after it.
/// </summary>
public static class ResultsClipboard
{
    /// <param name="headers">Column names, one per column of <paramref name="cells"/>.</param>
    /// <param name="cells">Rows of cell text. A null cell is one that isn't part of the selection and comes out empty.</param>
    /// <param name="includeHeaders">Put the column names on the first line.</param>
    public static string Build(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> cells, bool includeHeaders)
    {
        var text = new StringBuilder();

        if (includeHeaders)
            AppendLine(text, headers);

        foreach (var row in cells)
            AppendLine(text, row);

        // No trailing newline after the last row: pasting into a spreadsheet would otherwise land an extra empty row.
        if (text.Length > 0 && text[^1] == '\n')
            text.Length--;

        return text.ToString();
    }

    private static void AppendLine(StringBuilder text, IReadOnlyList<string?> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
                text.Append('\t');
            text.Append(Quote(values[i]));
        }

        text.Append('\n');
    }

    public static string Quote(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.IndexOfAny(new[] { '\t', '\r', '\n', '"' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
