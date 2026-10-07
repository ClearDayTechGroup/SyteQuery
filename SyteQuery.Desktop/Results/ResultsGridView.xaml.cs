using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        await ExportAsync("Excel Workbook (*.xlsx)|*.xlsx", "xlsx", vm => vm.ExportToExcel());

    private async void OnExportCsvClick(object sender, RoutedEventArgs e) =>
        await ExportAsync("CSV File (*.csv)|*.csv", "csv", vm => vm.ExportToCsv());

    private async void OnExportJsonClick(object sender, RoutedEventArgs e) =>
        await ExportAsync("JSON File (*.json)|*.json", "json", vm => vm.ExportToJson());

    private async Task ExportAsync(string filter, string extension, Func<ResultsGridViewModel, byte[]> generate)
    {
        if (ViewModel is not { HasData: true } vm)
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
