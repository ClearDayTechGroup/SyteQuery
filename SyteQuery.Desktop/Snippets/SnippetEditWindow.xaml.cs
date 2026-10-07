using System.Windows;
using SyteQuery.Features.QuerySnippets.Models;
using SyteQuery.Features.QuerySnippets.Services;

namespace SyteQuery.Desktop.Snippets;

/// <summary>
/// Shared Add/Edit form, ported from the old SnippetDialog.razor - one dialog, one code
/// path for both cases, distinguished only by whether <paramref name="original"/> is null.
/// </summary>
public partial class SnippetEditWindow : Window
{
    private readonly IQuerySnippetService _snippetService;
    private readonly QuerySnippet? _original;

    public SnippetEditWindow(IQuerySnippetService snippetService, IEnumerable<string> categories, QuerySnippet? original = null, string? initialQuery = null)
    {
        _snippetService = snippetService;
        _original = original;
        InitializeComponent();

        Title = original is null ? "Save as Snippet" : "Edit Snippet";

        foreach (var category in categories)
            CategoryBox.Items.Add(category);

        NameBox.Text = original?.Name ?? "";
        DescriptionBox.Text = original?.Description ?? "";
        CategoryBox.Text = original?.Category ?? "";
        QueryBox.Text = original?.Query ?? initialQuery ?? "";
    }

    private bool IsValid() =>
        !string.IsNullOrWhiteSpace(NameBox.Text) && !string.IsNullOrWhiteSpace(QueryBox.Text);

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!IsValid())
        {
            ErrorText.Text = "Name and Query are both required.";
            return;
        }

        SaveButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        ErrorText.Text = "";

        var now = DateTime.Now;
        var snippet = new QuerySnippet
        {
            Id = _original?.Id ?? Guid.NewGuid().ToString(),
            Name = NameBox.Text.Trim(),
            Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim(),
            Category = string.IsNullOrWhiteSpace(CategoryBox.Text) ? null : CategoryBox.Text.Trim(),
            Query = QueryBox.Text,
            CreatedAt = _original?.CreatedAt ?? now,
            ModifiedAt = _original is null ? null : now
        };

        try
        {
            await _snippetService.SaveAsync(snippet);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Failed to save: {ex.Message}";
            SaveButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
