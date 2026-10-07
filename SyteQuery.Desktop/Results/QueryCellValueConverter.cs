using System.Globalization;
using System.Windows.Data;

namespace SyteQuery.Desktop.Results;

/// <summary>One instance shared by every generated DataGridTextColumn - it's stateless.</summary>
public sealed class QueryCellValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        QueryCellFormatter.Format(value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("Query results are read-only.");
}
