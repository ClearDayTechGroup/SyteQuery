using SyteQuery.Features.Database.Entities;

namespace SyteQuery.Features.QueryHistory.Repositories;

public interface IQueryHistoryRepository
{
    Task<List<QueryHistoryDb>> GetByUserIdAsync(string userId, int limit = 100);
    Task<QueryHistoryDb?> GetByIdAsync(string userId, Guid historyId);
    Task<QueryHistoryDb> AddAsync(QueryHistoryDb history);
    Task DeleteAsync(string userId, Guid historyId);
    Task DeleteAllAsync(string userId);
    Task<int> GetCountByUserIdAsync(string userId);

    /// <summary>Permanently removes soft-deleted rows, rows older than the cutoff, and anything
    /// beyond the newest <paramref name="maxEntries"/>. Returns how many rows were removed.</summary>
    Task<int> PruneAsync(string userId, int maxEntries, DateTime olderThanUtc);

    /// <summary>Rewrites the database file so deleted rows actually give their space back
    /// (SQLite never shrinks the file on its own).</summary>
    Task VacuumAsync();
}
