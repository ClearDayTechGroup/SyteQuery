using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using SyteQuery.Features.DataExport.Services;
using SyteQuery.Features.QueryEditor.Models;

namespace SyteQuery.Desktop.Results;

/// <summary>One tab in the strip above the grid, for a command that returned several result sets.</summary>
public sealed record ResultSetTab(string Label);

/// <summary>
/// Ported from the old Blazor ResultsGrid.razor/.razor.cs, minus the "Limited View"
/// row/column-count cap entirely - that existed only to protect MudBlazor's DOM-based grid
/// from choking on large result sets, and a native WPF DataGrid with row/column
/// virtualization (see ResultsGridView.xaml) doesn't have that problem, so every row and
/// column is shown by default (matches the plan's "replacing the Limited View workaround
/// entirely" call for 4d).
///
/// A command can return several result sets. They show as tabs above the grid (only when there is
/// more than one); the grid, the search box and CSV/JSON export always work on the selected one,
/// while Excel export writes all of them, a sheet each.
/// </summary>
public sealed class ResultsGridViewModel : INotifyPropertyChanged
{
    private readonly ExcelExportService _excelExport;
    private readonly IDataExportService _dataExport;

    private QueryExecutionResult? _result;
    private List<QueryResultSet> _sets = new();
    private int _selectedIndex;
    private List<Dictionary<string, object?>> _rows = new();
    private ICollectionView _view;
    private string _searchText = "";
    private string _statusText = "Run a query to see results here.";
    private string? _errorText;

    public List<string> Columns { get; private set; } = new();

    /// <summary>The tab strip. Holds one entry per result set.</summary>
    public ObservableCollection<ResultSetTab> Tabs { get; } = new();

    /// <summary>Fired whenever the column set potentially changed (a new query ran, or another result set
    /// was selected) - the View rebuilds its DataGrid.Columns from this, since WPF's DataGrid has no
    /// built-in way to bind columns declaratively to a dynamic schema.</summary>
    public event Action? ColumnsChanged;

    public ICollectionView View
    {
        get => _view;
        private set => SetField(ref _view, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                View.Refresh();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    /// <summary>True when the selected result set has rows (enables CSV/JSON export).</summary>
    public bool HasData => _rows.Count > 0;

    /// <summary>True when any result set has rows (enables Excel export, which writes them all).</summary>
    public bool CanExportExcel => _sets.Any(s => s.RowCount > 0);

    public bool HasMultipleSets => _sets.Count > 1;

    public string ExcelToolTip => HasMultipleSets
        ? "Export every result set to Excel, one sheet each"
        : "Export to Excel";

    public string CsvToolTip => HasMultipleSets ? "Export the selected result set to CSV" : "Export to CSV";
    public string JsonToolTip => HasMultipleSets ? "Export the selected result set to JSON" : "Export to JSON";

    /// <summary>Which result set the grid shows (the index into <see cref="Tabs"/>).</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < 0 || value >= _sets.Count || value == _selectedIndex)
                return;

            _selectedIndex = value;
            OnPropertyChanged();
            ShowSelectedSet();
        }
    }

    /// <summary>The failure message when the last query failed.</summary>
    public string? ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetField(ref _errorText, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(ShowErrorOverlay));
                OnPropertyChanged(nameof(ShowErrorBanner));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorText);

    /// <summary>The failure replaces the grid when nothing came back before it, so the Results panel says
    /// so itself instead of just sitting empty.</summary>
    public bool ShowErrorOverlay => HasError && _sets.Count == 0;

    /// <summary>When a later statement failed, the result sets that did come back stay visible and the
    /// error shows in a bar above them.</summary>
    public bool ShowErrorBanner => HasError && _sets.Count > 0;

    public ResultsGridViewModel(ExcelExportService excelExport, IDataExportService dataExport)
    {
        _excelExport = excelExport;
        _dataExport = dataExport;
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = FilterRow;
    }

    /// <summary>Pass null to clear (nothing executed yet); pass a result even on failure so
    /// the grid can show "last query failed" instead of silently staying on stale data.</summary>
    public void SetResults(QueryExecutionResult? result)
    {
        _result = result;
        _sets = SetsOf(result);
        _selectedIndex = 0;

        Tabs.Clear();
        for (var i = 0; i < _sets.Count; i++)
            Tabs.Add(new ResultSetTab($"Result {i + 1}  ({_sets[i].RowCount} row{(_sets[i].RowCount == 1 ? "" : "s")})"));

        ErrorText = result is { Success: false } ? result.Message : null;

        OnPropertyChanged(nameof(SelectedIndex));
        OnPropertyChanged(nameof(HasMultipleSets));
        OnPropertyChanged(nameof(CanExportExcel));
        OnPropertyChanged(nameof(ExcelToolTip));
        OnPropertyChanged(nameof(CsvToolTip));
        OnPropertyChanged(nameof(JsonToolTip));
        OnPropertyChanged(nameof(ShowErrorOverlay));
        OnPropertyChanged(nameof(ShowErrorBanner));

        ShowSelectedSet();
    }

    private static List<QueryResultSet> SetsOf(QueryExecutionResult? result)
    {
        if (result is null)
            return new();

        // A failure keeps whatever came back before it (a later statement failed); a failure with
        // nothing before it has nothing to show.
        if (result.ResultSets.Count > 0)
            return result.ResultSets.ToList();

        if (result is { Success: true, Rows: not null })
        {
            return new()
            {
                new QueryResultSet
                {
                    Columns = result.Rows.Count > 0 ? result.Rows[0].Keys.ToList() : new List<string>(),
                    Rows = result.Rows
                }
            };
        }

        return new();
    }

    private void ShowSelectedSet()
    {
        var set = _sets.Count > 0 ? _sets[_selectedIndex] : null;
        _rows = set?.Rows ?? new();
        Columns = set is null ? new() : set.Columns.Count > 0 ? set.Columns.ToList() : (_rows.Count > 0 ? _rows[0].Keys.ToList() : new());

        var view = CollectionViewSource.GetDefaultView(_rows);
        view.Filter = FilterRow;
        View = view;

        StatusText = _result switch
        {
            null => "Run a query to see results here.",
            { Success: false } when _sets.Count == 0 => "Query failed",
            { Success: false } => $"Query failed after {_sets.Count} result set(s) - showing what came back",
            _ when _sets.Count == 0 => "Command complete - no result sets.",
            _ when _sets.Count > 1 => $"Result {_selectedIndex + 1} of {_sets.Count}: {_rows.Count} row(s), {Columns.Count} column(s)",
            _ when _rows.Count == 0 => "Query returned 0 row(s).",
            _ => $"{_rows.Count} row(s), {Columns.Count} column(s)"
        };

        OnPropertyChanged(nameof(HasData));
        ColumnsChanged?.Invoke();
    }

    private bool FilterRow(object obj)
    {
        if (string.IsNullOrWhiteSpace(_searchText))
            return true;

        if (obj is not Dictionary<string, object?> row)
            return true;

        foreach (var column in Columns)
        {
            if (row.TryGetValue(column, out var value) &&
                QueryCellFormatter.Format(value).Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // ------------------------------------------------------------
    // Export - byte[]-producing services unchanged from the Blazor version; only the
    // delivery mechanism differs (a native SaveFileDialog in the View's code-behind
    // instead of a JS interop "downloadFile" call).
    // ------------------------------------------------------------

    /// <summary>Every result set, a sheet each, when there are several; the single result set otherwise.</summary>
    public byte[] ExportToExcel()
    {
        if (_sets.Count <= 1)
            return _excelExport.ExportToExcel(_rows, "Query Results");

        var sheets = _sets
            .Select((s, i) => new ExcelSheet($"Result {i + 1}", s.Columns, s.Rows))
            .ToList();
        return _excelExport.ExportToExcel(sheets);
    }

    /// <summary>The selected result set.</summary>
    public byte[] ExportToCsv() => _dataExport.ExportToCsv(_rows);

    /// <summary>The selected result set.</summary>
    public byte[] ExportToJson() => _dataExport.ExportToJson(_rows);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
