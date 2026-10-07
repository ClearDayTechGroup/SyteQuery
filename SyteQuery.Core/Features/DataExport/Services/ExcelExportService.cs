using ClosedXML.Excel;
using System.Globalization;

namespace SyteQuery.Features.DataExport.Services;

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

        // Get column names from first row
        var columns = rows[0].Keys.ToList();

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

        // Convert to byte array
        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
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
