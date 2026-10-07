namespace SyteQuery.Features.QueryEditor.Models;

/// <summary>
/// Result from executing a query through the environment session manager.
/// </summary>
public sealed class QueryResult
{
    /// <summary>
    /// Indicates whether the query executed successfully.
    /// </summary>
    public bool Ok { get; init; }

    /// <summary>
    /// The query result data (may be Base64 encoded or plain JSON).
    /// </summary>
    public string? DataBase64 { get; init; }

    /// <summary>
    /// Status or error message from the execution.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Creates a successful query result.
    /// </summary>
    public static QueryResult Success(string? data = null, string? message = null) => new()
    {
        Ok = true,
        DataBase64 = data ?? string.Empty,
        Message = message ?? "Command complete"
    };

    /// <summary>
    /// Creates a failed query result.
    /// </summary>
    public static QueryResult Failure(string message) => new()
    {
        Ok = false,
        Message = message
    };
}