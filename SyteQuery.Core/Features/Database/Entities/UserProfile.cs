namespace SyteQuery.Features.Database.Entities;

/// <summary>
/// User profile and preferences. In this single-user desktop app there's exactly one
/// row here, tied to the seeded local user (see LocalUser/SeedDatabase) - kept as a
/// real entity rather than a flat settings file for now to minimize churn; revisit in
/// the Phase 3 local-storage pass.
/// </summary>
public class UserProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty; // FK to AspNetUsers
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Query Display Preferences
    /// <summary>
    /// When true, shows all columns and rows by default in query results (no "Limited View")
    /// </summary>
    public bool ShowAllDataByDefault { get; set; } = false;

    /// <summary>
    /// Full name for display
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();
}
