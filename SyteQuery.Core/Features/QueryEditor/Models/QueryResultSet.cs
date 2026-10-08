namespace SyteQuery.Features.QueryEditor.Models;

/// <summary>
/// One result set from a command - a command can produce several (one per SELECT).
/// </summary>
public sealed class QueryResultSet
{
    /// <summary>
    /// Column names in query order. Present even when there are no rows, so an empty result set still
    /// shows its headers.
    /// </summary>
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();

    /// <summary>The rows, each keyed by column name.</summary>
    public List<Dictionary<string, object?>> Rows { get; init; } = new();

    public int RowCount => Rows.Count;
}
