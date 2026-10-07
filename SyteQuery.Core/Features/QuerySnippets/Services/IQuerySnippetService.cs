using SyteQuery.Features.QuerySnippets.Models;

namespace SyteQuery.Features.QuerySnippets.Services;

/// <summary>
/// Service for managing saved query snippets
/// </summary>
public interface IQuerySnippetService
{
    /// <summary>
    /// Save a new snippet or update an existing one
    /// </summary>
    Task SaveAsync(QuerySnippet snippet);

    /// <summary>
    /// Get all snippets, ordered by name
    /// </summary>
    Task<List<QuerySnippet>> GetAllAsync();

    /// <summary>
    /// Get snippets by category
    /// </summary>
    Task<List<QuerySnippet>> GetByCategoryAsync(string category);

    /// <summary>
    /// Search snippets by name or description
    /// </summary>
    Task<List<QuerySnippet>> SearchAsync(string searchText);

    /// <summary>
    /// Get a specific snippet by ID
    /// </summary>
    Task<QuerySnippet?> GetByIdAsync(string id);

    /// <summary>
    /// Delete a snippet
    /// </summary>
    Task DeleteAsync(string id);

    /// <summary>
    /// Get all unique categories
    /// </summary>
    Task<List<string>> GetCategoriesAsync();

    /// <summary>
    /// Get the total count of snippets
    /// </summary>
    Task<int> GetCountAsync();
}
