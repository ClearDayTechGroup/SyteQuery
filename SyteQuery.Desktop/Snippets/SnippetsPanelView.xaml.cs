using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SyteQuery.Features.QuerySnippets.Models;

namespace SyteQuery.Desktop.Snippets;

public partial class SnippetsPanelView : UserControl
{
    public SnippetsPanelView()
    {
        InitializeComponent();
    }

    private SnippetsPanelViewModel? ViewModel => DataContext as SnippetsPanelViewModel;

    private void OnNewClick(object sender, RoutedEventArgs e) => OpenEditor(null);

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is QuerySnippet snippet)
            OpenEditor(snippet);
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (List.SelectedItem is QuerySnippet snippet)
            ViewModel?.RequestLoad(snippet);
    }

    private void OnLoadClick(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is QuerySnippet snippet)
            ViewModel?.RequestLoad(snippet);
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not QuerySnippet snippet || ViewModel is not { } vm)
            return;

        var confirm = MessageBox.Show(
            $"Delete snippet '{snippet.Name}'? This cannot be undone.",
            "Delete Snippet",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        await vm.DeleteAsync(snippet);
    }

    private void OnClearCategoryFilterClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
            vm.CategoryFilter = null;
    }

    private void OpenEditor(QuerySnippet? existing)
    {
        if (ViewModel is not { } vm)
            return;

        var dialog = new SnippetEditWindow(vm.SnippetService, vm.Categories, existing) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            _ = vm.RefreshAsync();
    }
}
