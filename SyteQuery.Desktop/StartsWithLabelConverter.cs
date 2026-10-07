using System.Globalization;
using System.Windows.Data;

namespace SyteQuery.Desktop;

/// <summary>
/// Object Explorer's search-mode toggle label: bool -> "Starts With" / "Contains".
/// </summary>
public sealed class StartsWithLabelConverter : IValueConverter
{
    public static readonly StartsWithLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Starts\nWith" : "Contains";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
