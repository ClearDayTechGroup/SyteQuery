namespace SyteQuery.Features.Environments.Services;

/// <summary>
/// Represents an environment connection profile.
/// </summary>
public sealed class EnvProfile
{
    /// <summary>
    /// Unique identifier for this profile.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name for this environment.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// IDO service URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Username for authentication.
    /// </summary>
    public string User { get; set; } = string.Empty;

    /// <summary>
    /// Password for authentication.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Configuration/database identifier.
    /// </summary>
    public string Config { get; set; } = string.Empty;

    /// <summary>
    /// Name of the IDO that runs this environment's queries. The user creates it in SyteLine
    /// (see SyteQuery.IDO/README.md); its method is always <c>ExecuteQuery</c>.
    /// </summary>
    public string IdoName { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether this environment is currently connected.
    /// </summary>
    public bool IsConnected { get; set; }
}