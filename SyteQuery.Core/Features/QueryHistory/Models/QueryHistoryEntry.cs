namespace SyteQuery.Features.QueryHistory.Models;

/// <summary>
/// Represents a query execution in the user's history
/// </summary>
public sealed record QueryHistoryEntry
{
    /// <summary>
    /// Unique identifier for this history entry
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The SQL query that was executed
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// When the query was executed
    /// </summary>
    public required DateTime ExecutedAt { get; init; }

    /// <summary>
    /// ID of the environment where query was executed
    /// </summary>
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// Name of the environment (for display, denormalized)
    /// </summary>
    public string? EnvironmentName { get; init; }

    /// <summary>
    /// Execution duration in milliseconds
    /// </summary>
    public long? DurationMs { get; init; }

    /// <summary>
    /// Number of rows returned
    /// </summary>
    public int? RowCount { get; init; }

    /// <summary>
    /// Whether the query succeeded
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if query failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}
