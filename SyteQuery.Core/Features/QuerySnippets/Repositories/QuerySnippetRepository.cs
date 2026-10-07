using SyteQuery.Features.Database.Data;
using SyteQuery.Features.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace SyteQuery.Features.QuerySnippets.Repositories;

public class QuerySnippetRepository : IQuerySnippetRepository
{
    private readonly ApplicationDbContext _context;

    public QuerySnippetRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<QuerySnippetDb>> GetByUserIdAsync(string userId)
    {
        return await _context.QuerySnippets
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<QuerySnippetDb?> GetByIdAsync(string userId, Guid snippetId)
    {
        return await _context.QuerySnippets
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SnippetId == snippetId && s.IsActive);
    }

    public async Task<QuerySnippetDb> AddAsync(QuerySnippetDb snippet)
    {
        _context.QuerySnippets.Add(snippet);
        await _context.SaveChangesAsync();
        return snippet;
    }

    public async Task UpdateAsync(QuerySnippetDb snippet)
    {
        snippet.ModifiedAt = DateTime.UtcNow;
        _context.QuerySnippets.Update(snippet);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string userId, Guid snippetId)
    {
        var snippet = await GetByIdAsync(userId, snippetId);
        if (snippet != null)
        {
            snippet.IsActive = false;
            snippet.ModifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetCountByUserIdAsync(string userId)
    {
        return await _context.QuerySnippets
            .CountAsync(s => s.UserId == userId && s.IsActive);
    }

    public async Task<List<string>> GetCategoriesByUserIdAsync(string userId)
    {
        return await _context.QuerySnippets
            .Where(s => s.UserId == userId && s.IsActive && s.Category != null)
            .Select(s => s.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }
}
