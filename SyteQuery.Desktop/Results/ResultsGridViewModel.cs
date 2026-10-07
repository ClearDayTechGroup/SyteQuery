using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using SyteQuery.Features.DataExport.Services;
using SyteQuery.Features.QueryEditor.Models;

namespace SyteQuery.Desktop.Results;

/// <summary>
/// Ported from the old Blazor ResultsGrid.razor/.razor.cs, minus the "Limited View"
/// row/column-count cap entirely - that existed only to protect MudBlazor's DOM-based grid
/// from choking on large result sets, and a native WPF DataGrid with row/column
/// virtualization (see ResultsGridView.xaml) doesn't have that problem, so every row and
/// column is shown by default (matches the plan's "replacing the Limited View workaround
/// entirely" call for 4d).
/// </summary>
public sealed class ResultsGridViewModel : INotifyPropertyChanged
{
    private readonly ExcelExportService _excelExport;
    private readonly IDataExportService _dataExport;

    private List<Dictionary<string, object?>> _rows = new();
    private ICollectionView _view;
    private string _searchText = "";
    private string _statusText = "Run a query to see results here.";
    private string? _errorText;

    public List<string> Columns { get; private set; } = new();

    /// <summary>Fired whenever the column set potentially changed (a new query ran) - the
    /// View rebuilds its DataGrid.Columns from this, since WPF's DataGrid has no built-in
    /// way to bind columns declaratively to a dynamic schema.</summary>
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

    public bool HasData => _rows.Count > 0;

    /// <summary>The failure message when the last query failed, shown in place of the grid so the
    /// Results panel says so itself instead of just sitting empty.</summary>
    public string? ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetField(ref _errorText, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorText);

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
        _rows = result is { Success: true } ? (result.Rows ?? new()) : new();
        Columns = _rows.Count > 0 ? _rows[0].Keys.ToList() : new();

        var view = CollectionViewSource.GetDefaultView(_rows);
        view.Filter = FilterRow;
        View = view;

        ErrorText = result is { Success: false } ? result.Message : null;

        StatusText = result switch
        {
            null => "Run a query to see results here.",
            { Success: false } => "Query failed",
            { RowCount: 0 } => "Query returned 0 row(s).",
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

    public byte[] ExportToExcel() => _excelExport.ExportToExcel(_rows, "Query Results");
    public byte[] ExportToCsv() => _dataExport.ExportToCsv(_rows);
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
