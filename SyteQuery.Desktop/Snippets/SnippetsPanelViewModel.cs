using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SyteQuery.Features.QuerySnippets.Models;
using SyteQuery.Features.QuerySnippets.Services;

namespace SyteQuery.Desktop.Snippets;

/// <summary>
/// Ported from the old QueryTool.razor.cs's snippet-management block (RefreshSnippetsAsync,
/// FilterSnippets, Save/Delete) - the old version lived as a tab inside QueryTool itself;
/// here it's its own docked AvalonDock panel (see MainWindow.xaml) so it stays visible
/// alongside the query editor instead of needing a tab switch, closer to how SSMS keeps
/// auxiliary panels docked rather than tabbed away.
/// </summary>
public sealed class SnippetsPanelViewModel : INotifyPropertyChanged
{
    private string _searchText = "";
    private string? _categoryFilter;

    public IQuerySnippetService SnippetService { get; }

    public ObservableCollection<QuerySnippet> Snippets { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    /// <summary>Fired when the user asks to load a snippet's query into the editor (Load
    /// button or double-click) - MainWindow wires this to the query editor.</summary>
    public event Action<string>? LoadRequested;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                _ = RefreshAsync();
        }
    }

    public string? CategoryFilter
    {
        get => _categoryFilter;
        set
        {
            if (SetField(ref _categoryFilter, value))
                _ = RefreshAsync();
        }
    }

    public SnippetsPanelViewModel(IQuerySnippetService snippetService)
    {
        SnippetService = snippetService;
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        var all = await SnippetService.GetAllAsync();
        var filtered = FilterSnippets(all, _searchText, _categoryFilter);

        Snippets.Clear();
        foreach (var snippet in filtered.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            Snippets.Add(snippet);

        var categories = await SnippetService.GetCategoriesAsync();
        Categories.Clear();
        foreach (var category in categories.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            Categories.Add(category);
    }

    public void RequestLoad(QuerySnippet snippet) => LoadRequested?.Invoke(snippet.Query);

    public async Task DeleteAsync(QuerySnippet snippet)
    {
        await SnippetService.DeleteAsync(snippet.Id);
        await RefreshAsync();
    }

    private static List<QuerySnippet> FilterSnippets(List<QuerySnippet> snippets, string searchText, string? categoryFilter)
    {
        var result = snippets.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(categoryFilter))
            result = result.Where(s => string.Equals(s.Category, categoryFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            result = result.Where(s =>
                s.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(s.Description) && s.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(s.Category) && s.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                s.Query.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        return result.ToList();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
