using SyteQuery.Features.Metadata.Models;

namespace SyteQuery.Features.Metadata.Services;

/// <summary>
/// Shared cache for database metadata to avoid duplicate queries between ObjectExplorer and IntelliSense
/// </summary>
public interface IMetadataCache
{
    /// <summary>
    /// Get cached tables or fetch from repository if not cached
    /// </summary>
    Task<List<DatabaseObject>> GetTablesAsync(string envId);

    /// <summary>
    /// Get cached views or fetch from repository if not cached
    /// </summary>
    Task<List<DatabaseObject>> GetViewsAsync(string envId);

    /// <summary>
    /// Get cached stored procedures or fetch from repository if not cached
    /// </summary>
    Task<List<DatabaseObject>> GetStoredProceduresAsync(string envId);

    /// <summary>
    /// Get cached scalar functions or fetch from repository if not cached
    /// </summary>
    Task<List<DatabaseObject>> GetScalarFunctionsAsync(string envId);

    /// <summary>
    /// Get cached table-valued functions or fetch from repository if not cached
    /// </summary>
    Task<List<DatabaseObject>> GetTableValuedFunctionsAsync(string envId);

    /// <summary>
    /// Get cached aggregate functions or fetch from repository if not cached
    /// </summary>
    Task<List<DatabaseObject>> GetAggregateFunctionsAsync(string envId);

    /// <summary>
    /// Get cached columns for a table or fetch from repository if not cached
    /// </summary>
    Task<List<ColumnInfo>> GetTableColumnsAsync(string envId, string schemaName, string tableName);

    /// <summary>
    /// Get cached columns for a view or fetch from repository if not cached
    /// </summary>
    Task<List<ColumnInfo>> GetViewColumnsAsync(string envId, string schemaName, string viewName);

    /// <summary>
    /// Get cached triggers for a table or fetch from repository if not cached
    /// </summary>
    Task<List<TriggerInfo>> GetTableTriggersAsync(string envId, string schemaName, string tableName);

    /// <summary>
    /// Clear cache for a specific environment
    /// </summary>
    void InvalidateEnvironment(string envId);

    /// <summary>
    /// Clear all cached data
    /// </summary>
    void InvalidateAll();
}
