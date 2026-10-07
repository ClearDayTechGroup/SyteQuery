using SyteQuery.Features.Database.Entities;
using SyteQuery.Features.QuerySnippets.Models;
using SyteQuery.Features.QuerySnippets.Repositories;
using SyteQuery.Features.Common.Services;

namespace SyteQuery.Features.QuerySnippets.Services;

public class DatabaseQuerySnippetService : BaseAuthenticatedService, IQuerySnippetService
{
    private readonly IQuerySnippetRepository _repository;

    public DatabaseQuerySnippetService(IQuerySnippetRepository repository)
    {
        _repository = repository;
    }

    public async Task SaveAsync(QuerySnippet snippet)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("User not authenticated");

        var snippetId = Guid.Parse(snippet.Id);
        var existing = await _repository.GetByIdAsync(userId, snippetId);

        if (existing == null)
        {
            // Create new snippet
            var newSnippet = new QuerySnippetDb
            {
                UserId = userId,
                SnippetId = snippetId,
                Name = snippet.Name,
                Description = snippet.Description,
                Category = snippet.Category,
                Query = snippet.Query,
                CreatedAt = snippet.CreatedAt,
                IsActive = true
            };

            await _repository.AddAsync(newSnippet);
        }
        else
        {
            // Update existing snippet
            existing.Name = snippet.Name;
            existing.Description = snippet.Description;
            existing.Category = snippet.Category;
            existing.Query = snippet.Query;

            await _repository.UpdateAsync(existing);
        }
    }

    public async Task<List<QuerySnippet>> GetAllAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return new List<QuerySnippet>();

        var snippets = await _repository.GetByUserIdAsync(userId);
        return snippets.Select(ToModel).ToList();
    }

    public async Task<List<QuerySnippet>> GetByCategoryAsync(string category)
    {
        var all = await GetAllAsync();
        return all.Where(s => s.Category == category).ToList();
    }

    public async Task<List<QuerySnippet>> SearchAsync(string searchText)
    {
        var all = await GetAllAsync();
        var searchLower = searchText.ToLowerInvariant();

        return all.Where(s =>
            s.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(s.Description) && s.Description.Contains(searchLower, StringComparison.OrdinalIgnoreCase)) ||
            s.Query.Contains(searchLower, StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }

    public async Task<QuerySnippet?> GetByIdAsync(string id)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return null;

        var snippet = await _repository.GetByIdAsync(userId, Guid.Parse(id));
        return snippet != null ? ToModel(snippet) : null;
    }

    public async Task DeleteAsync(string id)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("User not authenticated");

        await _repository.DeleteAsync(userId, Guid.Parse(id));
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return new List<string>();

        return await _repository.GetCategoriesByUserIdAsync(userId);
    }

    public async Task<int> GetCountAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return 0;

        return await _repository.GetCountByUserIdAsync(userId);
    }

    private static QuerySnippet ToModel(QuerySnippetDb db)
    {
        return new QuerySnippet
        {
            Id = db.SnippetId.ToString(),
            Name = db.Name,
            Description = db.Description,
            Category = db.Category,
            Query = db.Query,
            CreatedAt = db.CreatedAt,
            ModifiedAt = db.ModifiedAt
        };
    }
}
