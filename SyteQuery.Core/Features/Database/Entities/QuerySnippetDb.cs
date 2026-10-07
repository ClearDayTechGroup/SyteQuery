namespace SyteQuery.Features.Database.Entities;

public class QuerySnippetDb
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid SnippetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string Query { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
