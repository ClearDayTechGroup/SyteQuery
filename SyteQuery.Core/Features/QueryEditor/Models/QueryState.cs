using System.Collections.Generic;

namespace SyteQuery.Features.QueryEditor.Models;

/// <summary>
/// Manages query execution state.
/// </summary>
public sealed class QueryState
{
    private List<Dictionary<string, object?>>? _rows;
    private object _refreshKey = new();

    public List<Dictionary<string, object?>>? Rows => _rows;
    public object RefreshKey => _refreshKey;

    public void SetRows(List<Dictionary<string, object?>>? rows)
    {
        _rows = rows;
        _refreshKey = new(); // Force UI refresh
    }

    public void ClearRows() => SetRows(null);
}

/// <summary>
/// Message severity levels.
/// </summary>
public enum MessageSeverity
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Represents a message in the messages pane.
/// </summary>
public readonly record struct UiMessage(DateTime When, string Text, MessageSeverity Severity = MessageSeverity.Info);