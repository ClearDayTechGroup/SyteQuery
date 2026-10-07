namespace SyteQuery.Features.Database.Entities;

/// <summary>
/// The single local user this app runs as (see LocalUser). Kept as a real entity/table
/// - rather than removing the concept outright - purely so the existing UserId foreign
/// keys on UserProfile/UserEnvironmentDb/QuerySnippetDb/etc. keep working unchanged.
/// No longer an ASP.NET Identity type - there's no login, so no password/roles/claims.
/// </summary>
public class ApplicationUser
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
}
