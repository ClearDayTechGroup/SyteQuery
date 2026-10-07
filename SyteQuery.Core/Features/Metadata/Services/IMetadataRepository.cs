using SyteQuery.Features.Metadata.Models;

namespace SyteQuery.Features.Metadata.Services;

/// <summary>
/// Repository for database metadata queries (tables, views, stored procedures, functions, columns, triggers)
/// </summary>
public interface IMetadataRepository
{
    /// <summary>
    /// Get all tables from the database
    /// </summary>
    Task<List<DatabaseObject>> GetTablesAsync(string envId);

    /// <summary>
    /// Get all views from the database
    /// </summary>
    Task<List<DatabaseObject>> GetViewsAsync(string envId);

    /// <summary>
    /// Get all stored procedures from the database
    /// </summary>
    Task<List<DatabaseObject>> GetStoredProceduresAsync(string envId);

    /// <summary>
    /// Get scalar-valued functions from the database
    /// </summary>
    Task<List<DatabaseObject>> GetScalarFunctionsAsync(string envId);

    /// <summary>
    /// Get table-valued functions from the database
    /// </summary>
    Task<List<DatabaseObject>> GetTableValuedFunctionsAsync(string envId);

    /// <summary>
    /// Get aggregate functions from the database
    /// </summary>
    Task<List<DatabaseObject>> GetAggregateFunctionsAsync(string envId);

    /// <summary>
    /// Get all tables in a specific schema
    /// </summary>
    Task<List<DatabaseObject>> GetTablesBySchemaAsync(string envId, string schemaName);

    /// <summary>
    /// Get all views in a specific schema
    /// </summary>
    Task<List<DatabaseObject>> GetViewsBySchemaAsync(string envId, string schemaName);

    /// <summary>
    /// Get all stored procedures in a specific schema
    /// </summary>
    Task<List<DatabaseObject>> GetStoredProceduresBySchemaAsync(string envId, string schemaName);

    /// <summary>
    /// Get scalar-valued functions in a specific schema
    /// </summary>
    Task<List<DatabaseObject>> GetScalarFunctionsBySchemaAsync(string envId, string schemaName);

    /// <summary>
    /// Get table-valued functions in a specific schema
    /// </summary>
    Task<List<DatabaseObject>> GetTableValuedFunctionsBySchemaAsync(string envId, string schemaName);

    /// <summary>
    /// Get aggregate functions in a specific schema
    /// </summary>
    Task<List<DatabaseObject>> GetAggregateFunctionsBySchemaAsync(string envId, string schemaName);

    /// <summary>
    /// Get all columns for a specific table with their data types, nullability, and primary key information
    /// </summary>
    Task<List<ColumnInfo>> GetTableColumnsAsync(string envId, string schemaName, string tableName);

    /// <summary>
    /// Get all columns for a specific view with their data types and nullability
    /// </summary>
    Task<List<ColumnInfo>> GetViewColumnsAsync(string envId, string schemaName, string viewName);

    /// <summary>
    /// Get all triggers associated with a specific table
    /// </summary>
    Task<List<TriggerInfo>> GetTableTriggersAsync(string envId, string schemaName, string tableName);
}
