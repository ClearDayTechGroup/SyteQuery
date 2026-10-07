namespace SyteQuery.Features.QueryEditor.Models;

/// <summary>
/// Represents a single query tab, similar to SSMS query windows.
/// </summary>
public sealed class QueryTab
{
    private static int _nextTabNumber = 1;

    public string Id { get; init; }
    public string Title { get; set; }
    public string EditorHostId { get; init; }
    public string SqlContent { get; set; }
    public bool IsModified { get; set; }
    public QueryState QueryState { get; init; }

    public QueryTab(string? title = null)
    {
        Id = Guid.NewGuid().ToString();
        Title = title ?? $"Query {_nextTabNumber++}";
        EditorHostId = $"sqlEditorHost_{Id}";
        SqlContent = "";
        IsModified = false;
        QueryState = new QueryState();
    }

    public string DisplayTitle => IsModified ? $"{Title} *" : Title;
}
