using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;

namespace SyteQuery.Desktop.Results;

/// <summary>
/// Code-behind owns dynamic column generation (WPF's DataGrid has no declarative way to
/// bind columns to a schema that changes per query) and export file delivery (a native
/// SaveFileDialog + File.WriteAllBytesAsync, replacing the old JS interop "downloadFile"
/// call - same byte[]-producing export services underneath, just a different last step).
/// </summary>
public partial class ResultsGridView : UserControl
{
    private static readonly QueryCellValueConverter CellConverter = new();

    public ResultsGridView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Ctrl+C copies the selected cells (and only those), in the form ResultsClipboard builds.
        Grid.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy,
            (_, e) => { CopySelectedCells(includeHeaders: false); e.Handled = true; },
            (_, e) => { e.CanExecute = Grid.SelectedCells.Count > 0; e.Handled = true; }));
    }

    // ------------------------------------------------------------
    // Copy to the clipboard
    // ------------------------------------------------------------

    /// <summary>The column that was right-clicked (a cell or a header), for "Copy column".</summary>
    private DataGridColumn? _contextColumn;

    private static T? FindParent<T>(DependencyObject? from) where T : DependencyObject
    {
        while (from is not null)
        {
            if (from is T match)
                return match;

            from = System.Windows.Media.VisualTreeHelper.GetParent(from);
        }

        return null;
    }

    /// <summary>A right-click on a cell that isn't selected selects it first, like Excel; one inside the selection keeps the selection.</summary>
    private void OnGridRightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;

        if (FindParent<DataGridCell>(source) is { } cell)
        {
            _contextColumn = cell.Column;
            if (!cell.IsSelected)
            {
                Grid.SelectedCells.Clear();
                cell.IsSelected = true;
                cell.Focus();
            }
        }
        else
        {
            _contextColumn = FindParent<System.Windows.Controls.Primitives.DataGridColumnHeader>(source)?.Column;
        }
    }

    private void OnGridContextMenuOpened(object sender, RoutedEventArgs e)
    {
        var hasSelection = Grid.SelectedCells.Count > 0;
        CopyCellsItem.IsEnabled = hasSelection;
        CopyWithHeadersItem.IsEnabled = hasSelection;
        CopyRowsItem.IsEnabled = hasSelection;
        SelectAllItem.IsEnabled = Grid.Items.Count > 0;

        var column = _contextColumn;
        CopyColumnItem.IsEnabled = column is not null && Grid.Items.Count > 0;
        var name = column?.Header?.ToString();
        CopyColumnItem.Header = string.IsNullOrEmpty(name) ? "Copy col_umn" : $"Copy col_umn \"{(name.Length > 30 ? name[..30] + "…" : name)}\"";
    }

    private void OnCopyClick(object sender, RoutedEventArgs e) => CopySelectedCells(includeHeaders: false);
    private void OnCopyWithHeadersClick(object sender, RoutedEventArgs e) => CopySelectedCells(includeHeaders: true);
    private void OnCopyRowsClick(object sender, RoutedEventArgs e) => CopySelectedRows();
    private void OnCopyColumnClick(object sender, RoutedEventArgs e) => CopyColumn(_contextColumn);
    private void OnSelectAllClick(object sender, RoutedEventArgs e) { Grid.Focus(); Grid.SelectAllCells(); }

    /// <summary>Cell text as shown in the grid.</summary>
    private static string TextOf(object? item, DataGridColumn column) =>
        item is IDictionary<string, object?> row && column.Header?.ToString() is { } name && row.TryGetValue(name, out var value)
            ? QueryCellFormatter.Format(value)
            : "";

    /// <summary>
    /// The selected cells as a block: the rows and columns they touch, in the order shown. Cells in that block that
    /// aren't selected (a Ctrl-click selection with gaps) come out empty rather than shifting the others.
    /// </summary>
    private void CopySelectedCells(bool includeHeaders)
    {
        var selected = Grid.SelectedCells;
        if (selected.Count == 0)
            return;

        var rowIndex = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        var i = 0;
        foreach (var item in Grid.Items)
            rowIndex[item] = i++;

        var picked = new HashSet<(int Row, int Column)>();
        foreach (var cell in selected)
        {
            if (cell.Item is not null && cell.Column is not null && rowIndex.TryGetValue(cell.Item, out var r))
                picked.Add((r, cell.Column.DisplayIndex));
        }

        if (picked.Count == 0)
            return;

        var rows = picked.Select(p => p.Row).Distinct().OrderBy(r => r).ToList();
        var columns = picked.Select(p => p.Column).Distinct().OrderBy(c => c).ToList();
        var columnsByDisplay = Grid.Columns.ToDictionary(c => c.DisplayIndex);
        var items = Grid.Items.Cast<object>().ToList();

        var cells = rows.Select(r => (IReadOnlyList<string?>)columns
            .Select(c => picked.Contains((r, c)) ? TextOf(items[r], columnsByDisplay[c]) : null)
            .ToList()).ToList();
        var headers = columns.Select(c => columnsByDisplay[c].Header?.ToString() ?? "").ToList();

        SetClipboard(ResultsClipboard.Build(headers, cells, includeHeaders));
    }

    /// <summary>Every column of every row that has a selected cell.</summary>
    private void CopySelectedRows()
    {
        if (Grid.SelectedCells.Count == 0)
            return;

        var chosen = new HashSet<object>(Grid.SelectedCells.Where(c => c.Item is not null).Select(c => c.Item!), ReferenceEqualityComparer.Instance);
        var columns = Grid.Columns.OrderBy(c => c.DisplayIndex).ToList();

        // In the order shown, not the order the cells were selected in.
        var cells = Grid.Items.Cast<object>().Where(chosen.Contains)
            .Select(item => (IReadOnlyList<string?>)columns.Select(c => (string?)TextOf(item, c)).ToList()).ToList();

        SetClipboard(ResultsClipboard.Build(columns.Select(c => c.Header?.ToString() ?? "").ToList(), cells, includeHeaders: false));
    }

    /// <summary>Every value in one column (as filtered by the search box), one per line.</summary>
    private void CopyColumn(DataGridColumn? column)
    {
        if (column is null)
            return;

        var cells = Grid.Items.Cast<object>().Select(item => (IReadOnlyList<string?>)new[] { (string?)TextOf(item, column) }).ToList();
        SetClipboard(ResultsClipboard.Build(new[] { column.Header?.ToString() ?? "" }, cells, includeHeaders: false));
    }

    private static void SetClipboard(string text)
    {
        // The clipboard can be briefly locked by another program (a clipboard manager, Excel); retry rather than fail.
        try
        {
            Clipboard.SetDataObject(text, copy: true);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            MessageBox.Show("The clipboard is in use by another program. Try again in a moment.", "Copy", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private ResultsGridViewModel? ViewModel => DataContext as ResultsGridViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ResultsGridViewModel oldVm)
            oldVm.ColumnsChanged -= RebuildColumns;

        if (e.NewValue is ResultsGridViewModel newVm)
        {
            newVm.ColumnsChanged += RebuildColumns;
            RebuildColumns();
        }
    }

    private void RebuildColumns()
    {
        Grid.Columns.Clear();

        if (ViewModel is not { } vm)
            return;

        foreach (var columnName in vm.Columns)
        {
            Grid.Columns.Add(new DataGridTextColumn
            {
                Header = columnName,
                Binding = new Binding($"[{columnName}]") { Converter = CellConverter }
            });
        }
    }

    private async void OnExportExcelClick(object sender, RoutedEventArgs e) =>
        await ExportAsync("Excel Workbook (*.xlsx)|*.xlsx", "xlsx", vm => vm.CanExportExcel, vm => vm.ExportToExcel());

    private async void OnExportCsvClick(object sender, RoutedEventArgs e) =>
        await ExportAsync("CSV File (*.csv)|*.csv", "csv", vm => vm.HasData, vm => vm.ExportToCsv());

    private async void OnExportJsonClick(object sender, RoutedEventArgs e) =>
        await ExportAsync("JSON File (*.json)|*.json", "json", vm => vm.HasData, vm => vm.ExportToJson());

    private async Task ExportAsync(string filter, string extension, Func<ResultsGridViewModel, bool> canExport, Func<ResultsGridViewModel, byte[]> generate)
    {
        if (ViewModel is not { } vm || !canExport(vm))
            return;

        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = $"QueryResults_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var bytes = await Task.Run(() => generate(vm));
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
