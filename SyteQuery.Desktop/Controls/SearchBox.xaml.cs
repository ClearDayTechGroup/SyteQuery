using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SyteQuery.Desktop.Controls;

/// <summary>
/// A search field that is unmistakably a search field: magnifier icon, hint text while empty,
/// a clear (x) button once there's text, and Esc clears. Used by Object Explorer, the results
/// grid and the snippets panel so they all look and behave the same.
/// Bind <see cref="Text"/> like a TextBox's Text (use UpdateSourceTrigger=PropertyChanged for
/// search-as-you-type).
/// </summary>
public partial class SearchBox : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SearchBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(SearchBox), new PropertyMetadata("Search..."));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public SearchBox()
    {
        InitializeComponent();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        Text = string.Empty;
        Input.Focus();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !string.IsNullOrEmpty(Text))
        {
            Text = string.Empty;
            e.Handled = true;
        }
    }
}

/// <summary>true when the string has any text - drives the clear button's visibility.</summary>
public sealed class NotEmptyConverter : IValueConverter
{
    public static readonly NotEmptyConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
