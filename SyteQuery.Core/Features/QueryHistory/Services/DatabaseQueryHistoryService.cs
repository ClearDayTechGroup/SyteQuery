using SyteQuery.Features.Database.Entities;
using SyteQuery.Features.QueryHistory.Models;
using SyteQuery.Features.QueryHistory.Repositories;
using SyteQuery.Features.Common.Services;

namespace SyteQuery.Features.QueryHistory.Services;

public class DatabaseQueryHistoryService : BaseAuthenticatedService, IQueryHistoryService
{
    // Retention: history is a convenience, not an archive, and SQLite never shrinks its file on
    // its own - so the table is bounded by count, age and per-entry size.
    public const int MaxEntries = 1000;
    public const int RetentionDays = 90;
    public const int MaxTextLength = 50_000;

    private readonly IQueryHistoryRepository _repository;

    public DatabaseQueryHistoryService(IQueryHistoryRepository repository)
    {
        _repository = repository;
    }

    public async Task SaveAsync(QueryHistoryEntry entry)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("User not authenticated");

        var historyDb = new QueryHistoryDb
        {
            UserId = userId,
            HistoryId = Guid.Parse(entry.Id),
            Query = Truncate(entry.Query, "-- ... (query truncated for history)"),
            EnvironmentName = entry.EnvironmentName ?? string.Empty,
            ExecutedAt = entry.ExecutedAt,
            WasSuccessful = entry.Success,
            ErrorMessage = entry.ErrorMessage is null ? null : Truncate(entry.ErrorMessage, " ... (truncated)"),
            RowsAffected = entry.RowCount,
            IsActive = true
        };

        await _repository.AddAsync(historyDb);

        // Keep the table bounded as we go - two cheap indexed deletes. Never let a pruning problem
        // fail the save itself.
        try { await PruneAsync(); } catch { }
    }

    public async Task<int> PruneAsync(bool vacuumIfRemoved = false)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return 0;

        var removed = await _repository.PruneAsync(userId, MaxEntries, DateTime.UtcNow.AddDays(-RetentionDays));
        if (removed > 0 && vacuumIfRemoved)
            await _repository.VacuumAsync();

        return removed;
    }

    private static string Truncate(string text, string marker) =>
        text.Length <= MaxTextLength ? text : text[..MaxTextLength] + Environment.NewLine + marker;

    public async Task<List<QueryHistoryEntry>> GetAllAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return new List<QueryHistoryEntry>();

        var historyEntries = await _repository.GetByUserIdAsync(userId, MaxEntries);
        return historyEntries.Select(ToModel).ToList();
    }

    public async Task<List<QueryHistoryEntry>> GetByEnvironmentAsync(string environmentId)
    {
        var all = await GetAllAsync();
        return all.Where(h => h.EnvironmentId == environmentId).ToList();
    }

    public async Task<List<QueryHistoryEntry>> SearchAsync(string searchText)
    {
        var all = await GetAllAsync();
        var searchLower = searchText.ToLowerInvariant();

        return all.Where(h =>
            h.Query.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(h.EnvironmentName) && h.EnvironmentName.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    public async Task DeleteAsync(string id)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("User not authenticated");

        await _repository.DeleteAsync(userId, Guid.Parse(id));
    }

    public async Task ClearAllAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("User not authenticated");

        await _repository.DeleteAllAsync(userId);

        // A rare, user-initiated action - worth rewriting the file now so the space really comes back.
        try { await _repository.VacuumAsync(); } catch { }
    }

    public async Task<int> GetCountAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return 0;

        return await _repository.GetCountByUserIdAsync(userId);
    }

    private static QueryHistoryEntry ToModel(QueryHistoryDb db)
    {
        return new QueryHistoryEntry
        {
            Id = db.HistoryId.ToString(),
            Query = db.Query,
            ExecutedAt = db.ExecutedAt,
            EnvironmentId = string.Empty, // We don't store this in the DB currently
            EnvironmentName = db.EnvironmentName,
            Success = db.WasSuccessful,
            ErrorMessage = db.ErrorMessage,
            RowCount = db.RowsAffected,
            DurationMs = null // We don't store this in the DB currently
        };
    }
}
