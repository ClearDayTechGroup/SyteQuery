using System.Collections.Generic;

namespace SyteQuery.Features.QueryEditor.Models;

/// <summary>
/// High-level result from query execution through IdoQueryService.
/// Includes parsed data rows and optional debug information.
/// </summary>
public sealed class QueryExecutionResult
{
    /// <summary>
    /// Indicates whether the query executed and parsed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Parsed result rows (null if no data or error).
    /// </summary>
    public List<Dictionary<string, object?>>? Rows { get; init; }

    /// <summary>
    /// Status or error message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional debug information (only populated if requested).
    /// </summary>
    public string? DebugInfo { get; init; }

    /// <summary>
    /// Gets the number of rows returned (0 if no data).
    /// </summary>
    public int RowCount => Rows?.Count ?? 0;

    /// <summary>
    /// Gets whether any data was returned.
    /// </summary>
    public bool HasData => Rows is not null && Rows.Count > 0;
}