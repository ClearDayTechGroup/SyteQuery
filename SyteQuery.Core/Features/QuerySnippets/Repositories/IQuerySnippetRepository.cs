using SyteQuery.Features.Database.Entities;

namespace SyteQuery.Features.QuerySnippets.Repositories;

public interface IQuerySnippetRepository
{
    Task<List<QuerySnippetDb>> GetByUserIdAsync(string userId);
    Task<QuerySnippetDb?> GetByIdAsync(string userId, Guid snippetId);
    Task<QuerySnippetDb> AddAsync(QuerySnippetDb snippet);
    Task UpdateAsync(QuerySnippetDb snippet);
    Task DeleteAsync(string userId, Guid snippetId);
    Task<int> GetCountByUserIdAsync(string userId);
    Task<List<string>> GetCategoriesByUserIdAsync(string userId);
}
