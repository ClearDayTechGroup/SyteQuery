using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SyteQuery.Desktop.History;

public partial class HistoryPanelView : UserControl
{
    public HistoryPanelView()
    {
        InitializeComponent();
    }

    private HistoryPanelViewModel? ViewModel => DataContext as HistoryPanelViewModel;

    private void OnLoadClick(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is HistoryItem item)
            ViewModel?.RequestLoad(item);
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (List.SelectedItem is HistoryItem item)
            ViewModel?.RequestLoad(item);
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is HistoryItem item && ViewModel is { } vm)
            await vm.DeleteAsync(item);
    }

    private async void OnClearAllClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
            return;

        var confirm = MessageBox.Show(
            "Clear all query history? This cannot be undone.",
            "Clear History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
            await vm.ClearAllAsync();
    }
}
