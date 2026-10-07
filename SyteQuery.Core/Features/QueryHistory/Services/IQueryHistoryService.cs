using SyteQuery.Features.QueryHistory.Models;

namespace SyteQuery.Features.QueryHistory.Services;

/// <summary>
/// Service for managing query execution history
/// </summary>
public interface IQueryHistoryService
{
    /// <summary>
    /// Save a query to history
    /// </summary>
    Task SaveAsync(QueryHistoryEntry entry);

    /// <summary>
    /// Get all history entries, ordered by execution time (newest first)
    /// </summary>
    Task<List<QueryHistoryEntry>> GetAllAsync();

    /// <summary>
    /// Get history entries for a specific environment
    /// </summary>
    Task<List<QueryHistoryEntry>> GetByEnvironmentAsync(string environmentId);

    /// <summary>
    /// Search history by query text
    /// </summary>
    Task<List<QueryHistoryEntry>> SearchAsync(string searchText);

    /// <summary>
    /// Delete a specific history entry
    /// </summary>
    Task DeleteAsync(string id);

    /// <summary>
    /// Clear all history
    /// </summary>
    Task ClearAllAsync();

    /// <summary>
    /// Get the total count of history entries
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>Applies the retention limits (newest entries only, nothing older than the age
    /// cap). Returns how many entries were removed; when <paramref name="vacuumIfRemoved"/> is set
    /// and something was removed, also shrinks the database file.</summary>
    Task<int> PruneAsync(bool vacuumIfRemoved = false);
}
