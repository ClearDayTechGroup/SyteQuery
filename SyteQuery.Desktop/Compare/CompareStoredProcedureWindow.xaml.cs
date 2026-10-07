using System.Windows;
using SyteQuery.Features.Environments.Services;
using SyteQuery.Features.ObjectExplorer.Services;

namespace SyteQuery.Desktop.Compare;

/// <summary>
/// Ported from the old CompareStoredProcedureDialog: pick two environments, fetch the same
/// stored procedure's definition from each, and show a side-by-side line diff. Rows are
/// paired in a single list so both sides scroll together without any scroll-sync code
/// (the web version needed a JS helper for that).
/// </summary>
public partial class CompareStoredProcedureWindow : Window
{
    private readonly IObjectDefinitionService _definitions;
    private readonly string _procName;

    public CompareStoredProcedureWindow(
        IObjectDefinitionService definitions,
        IReadOnlyList<EnvProfile> environments,
        string procName,
        string? defaultEnvironmentId)
    {
        _definitions = definitions;
        _procName = procName;
        InitializeComponent();

        TitleText.Text = $"Compare {procName}";
        EnvABox.ItemsSource = environments;
        EnvBBox.ItemsSource = environments;

        var a = environments.FirstOrDefault(e => e.Id == defaultEnvironmentId) ?? environments.FirstOrDefault();
        var b = environments.FirstOrDefault(e => e.Id != a?.Id) ?? a;
        EnvABox.SelectedValue = a?.Id;
        EnvBBox.SelectedValue = b?.Id;
    }

    private async void OnCompareClick(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        SummaryText.Text = "";
        DiffList.ItemsSource = null;

        var envA = EnvABox.SelectedValue as string;
        var envB = EnvBBox.SelectedValue as string;

        if (string.IsNullOrWhiteSpace(envA) || string.IsNullOrWhiteSpace(envB))
        {
            ErrorText.Text = "Select two environments to compare.";
            return;
        }

        if (string.Equals(envA, envB, StringComparison.OrdinalIgnoreCase))
        {
            ErrorText.Text = "Please select two different environments.";
            return;
        }

        CompareButton.IsEnabled = false;
        try
        {
            var leftTask = _definitions.GetStoredProcedureDefinitionAsync(envA, _procName);
            var rightTask = _definitions.GetStoredProcedureDefinitionAsync(envB, _procName);
            await Task.WhenAll(leftTask, rightTask);

            var left = leftTask.Result;
            var right = rightTask.Result;

            if (!left.Success || string.IsNullOrWhiteSpace(left.Definition))
            {
                ErrorText.Text = left.ErrorMessage ?? "Failed to load definition from Environment A.";
                return;
            }

            if (!right.Success || string.IsNullOrWhiteSpace(right.Definition))
            {
                ErrorText.Text = right.ErrorMessage ?? "Failed to load definition from Environment B.";
                return;
            }

            var rows = LineDiff.Compute(left.Definition, right.Definition, IgnoreWhitespaceBox.IsChecked == true);
            DiffList.ItemsSource = rows;

            var removed = rows.Count(r => r.Kind == DiffKind.LeftOnly);
            var added = rows.Count(r => r.Kind == DiffKind.RightOnly);
            SummaryText.Text = removed == 0 && added == 0
                ? "No differences."
                : $"{removed} line(s) only in A, {added} line(s) only in B.";
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Compare failed: {ex.Message}";
        }
        finally
        {
            CompareButton.IsEnabled = true;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
