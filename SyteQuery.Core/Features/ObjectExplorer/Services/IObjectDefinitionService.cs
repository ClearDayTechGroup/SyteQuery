namespace SyteQuery.Features.ObjectExplorer.Services;

/// <summary>
/// Service for retrieving SQL object definitions (stored procedures, functions, views)
/// </summary>
public interface IObjectDefinitionService
{
    /// <summary>
    /// Get the definition of a stored procedure
    /// </summary>
    Task<ObjectDefinitionResult> GetStoredProcedureDefinitionAsync(string envId, string objectName);

    /// <summary>
    /// Get the definition of a function (scalar, table-valued, or aggregate)
    /// </summary>
    Task<ObjectDefinitionResult> GetFunctionDefinitionAsync(string envId, string objectName);

    /// <summary>
    /// Get the definition of a view
    /// </summary>
    Task<ObjectDefinitionResult> GetViewDefinitionAsync(string envId, string objectName);

    /// <summary>
    /// Get the definition of a trigger
    /// </summary>
    Task<ObjectDefinitionResult> GetTriggerDefinitionAsync(string envId, string objectName);

    /// <summary>
    /// Get the script for a table (columns, keys, indexes, triggers)
    /// </summary>
    Task<ObjectDefinitionResult> GetTableScriptAsync(string envId, string schemaName, string tableName);
}

/// <summary>
/// Result of retrieving an object definition
/// </summary>
public sealed record ObjectDefinitionResult(bool Success, string? Definition, string? ErrorMessage);
