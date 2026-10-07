namespace SyteQuery.Features.Database.Entities;

/// <summary>
/// Comprehensive audit log for all user actions
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; } // FK to AspNetUsers (nullable for anonymous actions)
    public string Action { get; set; } = string.Empty; // Login, Logout, QueryExecute, SnippetCreate, etc.
    public string? EntityType { get; set; } // Query, Snippet, Environment, User
    public string? EntityId { get; set; }
    public string? Details { get; set; } // JSON with additional context
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
