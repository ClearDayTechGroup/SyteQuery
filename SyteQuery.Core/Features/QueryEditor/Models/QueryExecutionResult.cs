using System.Collections.Generic;

namespace SyteQuery.Features.QueryEditor.Models;

/// <summary>
/// High-level result from query execution through IdoQueryService.
/// Includes parsed data rows and optional debug information.
/// </summary>
public sealed class QueryExecutionResult
{
    /// <summary>
    /// Indicates whether the query executed and parsed successfully. A command that failed part-way
    /// (say its second statement errored) is not successful, but <see cref="ResultSets"/> still holds
    /// the result sets read before the failure.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Parsed rows of the first result set (null if no data or error). Kept so single-result callers
    /// don't have to know about <see cref="ResultSets"/>.
    /// </summary>
    public List<Dictionary<string, object?>>? Rows { get; init; }

    /// <summary>
    /// Every result set the command produced, in order (empty when it produced none).
    /// </summary>
    public IReadOnlyList<QueryResultSet> ResultSets { get; init; } = Array.Empty<QueryResultSet>();

    /// <summary>
    /// The query IDO's output format version (1 = the original single-result IDO, 2 = multiple result
    /// sets), or null if the response didn't say. Used to tell the user when their environment's IDO is
    /// out of date.
    /// </summary>
    public int? IdoVersion { get; init; }

    /// <summary>
    /// Status or error message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional debug information (only populated if requested).
    /// </summary>
    public string? DebugInfo { get; init; }

    /// <summary>
    /// Gets the number of rows returned in the first result set (0 if no data).
    /// </summary>
    public int RowCount => Rows?.Count ?? 0;

    /// <summary>
    /// Gets the number of rows returned across all result sets.
    /// </summary>
    public int TotalRowCount => ResultSets.Count > 0 ? ResultSets.Sum(s => s.RowCount) : RowCount;

    /// <summary>
    /// Gets whether any data was returned.
    /// </summary>
    public bool HasData => Rows is not null && Rows.Count > 0;
}
