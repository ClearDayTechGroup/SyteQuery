using ClosedXML.Excel;
using System.Globalization;

namespace SyteQuery.Features.DataExport.Services;

/// <summary>One worksheet of a multi-result export.</summary>
/// <param name="Name">Sheet name (Excel allows 31 characters and none of []:*?/\).</param>
/// <param name="Columns">Column headers, written even when there are no rows.</param>
/// <param name="Rows">The rows.</param>
public sealed record ExcelSheet(string Name, IReadOnlyList<string> Columns, List<Dictionary<string, object?>> Rows);

public class ExcelExportService
{
    public byte[] ExportToExcel(List<Dictionary<string, object?>> rows, string sheetName = "Results")
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        if (rows == null || rows.Count == 0)
        {
            worksheet.Cell(1, 1).Value = "No data to export";
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        WriteSheet(worksheet, rows[0].Keys.ToList(), rows);

        // Convert to byte array
        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>One workbook, one sheet per result set - used when a command returned several.</summary>
    public byte[] ExportToExcel(IReadOnlyList<ExcelSheet> sheets)
    {
        using var workbook = new XLWorkbook();

        foreach (var sheet in sheets)
        {
            var worksheet = workbook.Worksheets.Add(SafeSheetName(sheet.Name, workbook));
            WriteSheet(worksheet, sheet.Columns, sheet.Rows);
        }

        if (sheets.Count == 0)
            workbook.Worksheets.Add("Results").Cell(1, 1).Value = "No data to export";

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static string SafeSheetName(string name, XLWorkbook workbook)
    {
        var cleaned = new string(name.Where(c => "[]:*?/\\".IndexOf(c) < 0).ToArray()).Trim();
        if (cleaned.Length == 0)
            cleaned = "Results";
        if (cleaned.Length > 31)
            cleaned = cleaned[..31];

        var candidate = cleaned;
        var n = 2;
        while (workbook.Worksheets.Contains(candidate))
        {
            var suffix = $" ({n++})";
            candidate = cleaned[..Math.Min(cleaned.Length, 31 - suffix.Length)] + suffix;
        }

        return candidate;
    }

    private static void WriteSheet(IXLWorksheet worksheet, IReadOnlyList<string> columns, List<Dictionary<string, object?>> rows)
    {
        // Write headers
        for (int i = 0; i < columns.Count; i++)
        {
            var headerCell = worksheet.Cell(1, i + 1);
            headerCell.Value = columns[i];
            headerCell.Style.Font.Bold = true;
            headerCell.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerCell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        // Write data rows
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            for (int colIndex = 0; colIndex < columns.Count; colIndex++)
            {
                var columnName = columns[colIndex];
                var cell = worksheet.Cell(rowIndex + 2, colIndex + 1);

                if (row.TryGetValue(columnName, out var value) && value != null)
                {
                    // Handle different data types
                    var cellValue = FormatCellValue(value);

                    // Try to parse as number or date for better Excel formatting
                    if (double.TryParse(cellValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var numValue))
                    {
                        // Check for NaN and Infinity - Excel can't handle these
                        if (double.IsNaN(numValue) || double.IsInfinity(numValue))
                        {
                            cell.Value = cellValue; // Fall back to string representation
                        }
                        else
                        {
                            cell.Value = numValue;
                        }
                    }
                    else if (DateTime.TryParse(cellValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
                    {
                        // Check if date is within Excel's valid range (1900-01-01 to 9999-12-31)
                        if (dateValue >= new DateTime(1900, 1, 1) && dateValue <= new DateTime(9999, 12, 31))
                        {
                            cell.Value = dateValue;
                            cell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";
                        }
                        else
                        {
                            cell.Value = cellValue; // Fall back to string representation for out-of-range dates
                        }
                    }
                    else
                    {
                        cell.Value = cellValue;
                    }
                }
            }
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Freeze header row
        worksheet.SheetView.FreezeRows(1);
    }

    private static string FormatCellValue(object? value)
    {
        if (value == null)
            return string.Empty;

        if (value is Newtonsoft.Json.Linq.JValue jv)
            return Convert.ToString(jv.Value, CultureInfo.InvariantCulture) ?? string.Empty;

        if (value is Newtonsoft.Json.Linq.JToken jt)
            return jt.ToString(Newtonsoft.Json.Formatting.None);

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
