using SyteQuery.Features.IntelliSense.Models;

namespace SyteQuery.Features.IntelliSense.Services;

/// <summary>
/// Provides IntelliSense data for SQL editor autocomplete
/// </summary>
public interface IIntelliSenseProvider
{
    /// <summary>
    /// Get all available completion items for the current environment
    /// </summary>
    Task<IntelliSenseData> GetCompletionDataAsync(string envId);

    /// <summary>
    /// Get columns for a specific table (for context-aware completion)
    /// </summary>
    Task<List<ColumnCompletionItem>> GetTableColumnsAsync(string envId, string tableName);

    /// <summary>
    /// Clear cached data for a specific environment
    /// </summary>
    void ClearCache(string envId);

    /// <summary>
    /// Clear all cached data
    /// </summary>
    void ClearAllCache();
}
