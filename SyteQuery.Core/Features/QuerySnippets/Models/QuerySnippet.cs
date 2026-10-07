namespace SyteQuery.Features.QuerySnippets.Models;

/// <summary>
/// Represents a saved query snippet that users can reuse
/// </summary>
public sealed record QuerySnippet
{
    /// <summary>
    /// Unique identifier for this snippet
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// User-friendly name for the snippet
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional description of what the snippet does
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The SQL query content
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Optional category for organizing snippets (e.g., "Reports", "Common Queries")
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// When the snippet was created
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the snippet was last modified
    /// </summary>
    public DateTime? ModifiedAt { get; init; }
}
