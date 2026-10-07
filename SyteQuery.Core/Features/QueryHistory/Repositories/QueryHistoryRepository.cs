using SyteQuery.Features.Database.Data;
using SyteQuery.Features.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace SyteQuery.Features.QueryHistory.Repositories;

public class QueryHistoryRepository : IQueryHistoryRepository
{
    private readonly ApplicationDbContext _context;

    public QueryHistoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<QueryHistoryDb>> GetByUserIdAsync(string userId, int limit = 100)
    {
        return await _context.QueryHistory
            .Where(h => h.UserId == userId && h.IsActive)
            .OrderByDescending(h => h.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<QueryHistoryDb?> GetByIdAsync(string userId, Guid historyId)
    {
        return await _context.QueryHistory
            .FirstOrDefaultAsync(h => h.UserId == userId && h.HistoryId == historyId && h.IsActive);
    }

    public async Task<QueryHistoryDb> AddAsync(QueryHistoryDb history)
    {
        history.ExecutedAt = DateTime.UtcNow;
        _context.QueryHistory.Add(history);
        await _context.SaveChangesAsync();
        return history;
    }

    // Real deletes, not an IsActive flag: flagging left the row (and its full query text) in the
    // file forever, so "clear history" never reclaimed any space.
    public async Task DeleteAsync(string userId, Guid historyId)
    {
        await _context.QueryHistory
            .Where(h => h.UserId == userId && h.HistoryId == historyId)
            .ExecuteDeleteAsync();
    }

    public async Task DeleteAllAsync(string userId)
    {
        await _context.QueryHistory
            .Where(h => h.UserId == userId)
            .ExecuteDeleteAsync();
    }

    public async Task<int> GetCountByUserIdAsync(string userId)
    {
        return await _context.QueryHistory
            .CountAsync(h => h.UserId == userId && h.IsActive);
    }

    public async Task<int> PruneAsync(string userId, int maxEntries, DateTime olderThanUtc)
    {
        // Soft-deleted rows from before real deletes existed, plus anything past retention.
        var removed = await _context.QueryHistory
            .Where(h => h.UserId == userId && (!h.IsActive || h.ExecutedAt < olderThanUtc))
            .ExecuteDeleteAsync();

        // Then keep only the newest N: find the (N+1)th newest timestamp and drop it and older.
        var cutoff = await _context.QueryHistory
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ExecutedAt)
            .Skip(maxEntries)
            .Select(h => (DateTime?)h.ExecutedAt)
            .FirstOrDefaultAsync();

        if (cutoff is not null)
        {
            removed += await _context.QueryHistory
                .Where(h => h.UserId == userId && h.ExecutedAt <= cutoff)
                .ExecuteDeleteAsync();
        }

        return removed;
    }

    public async Task VacuumAsync()
    {
        await _context.Database.ExecuteSqlRawAsync("VACUUM;");
    }
}
