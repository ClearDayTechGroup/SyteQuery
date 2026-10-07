namespace SyteQuery.Features.Database.Entities;

public class UserEnvironmentDb
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid EnvironmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ConfigName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;

    /// <summary>Name of the IDO this environment runs queries through (the user creates it in
    /// SyteLine - see SyteQuery.IDO/README.md). Existing rows were migrated to "ue_RC_QueryTool".</summary>
    public string IdoName { get; set; } = string.Empty;

    public string EncryptedPassword { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
