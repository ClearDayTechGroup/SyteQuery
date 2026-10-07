using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SyteQuery.Desktop.Snippets;

/// <summary>Hides a snippet list item's Category/Description line when that field is blank,
/// rather than showing an empty line.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public static readonly StringToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
