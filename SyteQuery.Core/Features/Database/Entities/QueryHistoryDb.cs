namespace SyteQuery.Features.Database.Entities;

public class QueryHistoryDb
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid HistoryId { get; set; }
    public string Query { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public bool WasSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public int? RowsAffected { get; set; }
    public bool IsActive { get; set; } = true;
}
