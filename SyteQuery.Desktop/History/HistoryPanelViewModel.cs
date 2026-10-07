using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SyteQuery.Features.QueryHistory.Models;
using SyteQuery.Features.QueryHistory.Services;

namespace SyteQuery.Desktop.History;

/// <summary>One row in the history list - the stored entry plus the display text derived from it.</summary>
public sealed class HistoryItem
{
    public HistoryItem(QueryHistoryEntry entry)
    {
        Entry = entry;

        // First lines of the query, whitespace collapsed, so a multi-line query reads as one short line.
        var collapsed = Regex.Replace(entry.Query, @"\s+", " ").Trim();
        Preview = collapsed.Length > 160 ? collapsed[..160] + "..." : collapsed;

        // The DB stores UTC; show local time.
        var local = DateTime.SpecifyKind(entry.ExecutedAt, DateTimeKind.Utc).ToLocalTime();
        WhenText = local.ToString("MMM d, h:mm tt");

        IsFailed = !entry.Success;
        var outcome = entry.Success
            ? (entry.RowCount is { } rows ? $"{rows:N0} row(s)" : "ok")
            : "failed";
        Detail = string.IsNullOrWhiteSpace(entry.EnvironmentName) ? outcome : $"{entry.EnvironmentName} - {outcome}";
    }

    public QueryHistoryEntry Entry { get; }
    public string Preview { get; }
    public string WhenText { get; }
    public string Detail { get; }
    public bool IsFailed { get; }
}

/// <summary>
/// Ported from the old QueryTool.razor.cs history block (RefreshHistoryAsync /
/// LoadHistoryEntryAsync / ClearHistoryAsync) as a docked panel, like Snippets. Search is done
/// in memory over the (retention-capped) list rather than another database query.
/// </summary>
public sealed class HistoryPanelViewModel : INotifyPropertyChanged
{
    private readonly IQueryHistoryService _history;
    private List<HistoryItem> _all = new();
    private string _searchText = "";
    private string _statusText = "";

    public ObservableCollection<HistoryItem> Items { get; } = new();

    /// <summary>Fired when the user asks to load an entry back into an editor tab.</summary>
    public event Action<QueryHistoryEntry>? LoadRequested;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                ApplyFilter();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public HistoryPanelViewModel(IQueryHistoryService history)
    {
        _history = history;
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            _all = (await _history.GetAllAsync()).Select(e => new HistoryItem(e)).ToList();
        }
        catch
        {
            _all = new List<HistoryItem>();
        }

        ApplyFilter();
    }

    public void RequestLoad(HistoryItem item) => LoadRequested?.Invoke(item.Entry);

    public async Task DeleteAsync(HistoryItem item)
    {
        await _history.DeleteAsync(item.Entry.Id);
        await RefreshAsync();
    }

    public async Task ClearAllAsync()
    {
        await _history.ClearAllAsync();
        await RefreshAsync();
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(_searchText)
            ? _all
            : _all.Where(i =>
                    i.Entry.Query.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    (i.Entry.EnvironmentName?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

        Items.Clear();
        foreach (var item in filtered)
            Items.Add(item);

        StatusText = string.IsNullOrWhiteSpace(_searchText)
            ? $"{_all.Count} entr{(_all.Count == 1 ? "y" : "ies")}"
            : $"{filtered.Count} of {_all.Count}";
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
