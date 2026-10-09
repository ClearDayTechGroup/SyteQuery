// IEnvironmentSessionManager.cs
using SyteQuery.Features.QueryEditor.Models;

namespace SyteQuery.Features.Environments.Services;

public interface IEnvironmentSessionManager
{
    /// <summary>
    /// Gets all registered environment profiles.
    /// </summary>
    IReadOnlyList<EnvProfile> Profiles { get; }

    /// <summary>
    /// Event raised when profiles are added, updated, or removed.
    /// </summary>
    event Action? ProfilesChanged;

    /// <summary>
    /// Adds a new environment profile.
    /// </summary>
    /// <exception cref="ArgumentException">If profile ID is invalid.</exception>
    /// <exception cref="InvalidOperationException">If profile already exists.</exception>
    Task<EnvProfile> AddAsync(EnvProfile profile);

    /// <summary>
    /// Updates an existing environment profile.
    /// </summary>
    /// <exception cref="InvalidOperationException">If profile not found.</exception>
    Task UpdateAsync(EnvProfile profile);

    /// <summary>
    /// Removes an environment profile and disconnects its session.
    /// </summary>
    Task RemoveAsync(string envId);

    /// <summary>
    /// Establishes a connection to the specified environment.
    /// </summary>
    /// <exception cref="InvalidOperationException">If environment not found or validation fails.</exception>
    Task ConnectAsync(string envId, CancellationToken ct = default);

    /// <summary>
    /// Closes the connection to the specified environment.
    /// </summary>
    Task DisconnectAsync(string envId);

    /// <summary>
    /// Executes a query against the specified environment.
    /// </summary>
    /// <returns>Result containing data and status information.</returns>
    Task<QueryResult> ExecuteAsync(string envId, string cmd, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the list of tables from the specified environment (cached).
    /// </summary>
    Task<IReadOnlyList<string>> GetTablesAsync(string envId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the list of views from the specified environment (cached).
    /// </summary>
    Task<IReadOnlyList<string>> GetViewsAsync(string envId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the list of stored procedures from the specified environment (cached).
    /// </summary>
    Task<IReadOnlyList<string>> GetProcsAsync(string envId, CancellationToken ct = default);

    /// <summary>
    /// Loads profiles from storage into memory. Must be called before accessing Profiles property.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Resolves the environment's connection details and a valid (cached or freshly
    /// acquired) REST security token, then runs <paramref name="action"/> against them.
    /// Replaces the old Mongoose <c>Client</c> session model - REST v2 is stateless, so
    /// there's no explicit open/close, just "give me a URL and a token that works."
    /// </summary>
    Task<T> UseSessionAsync<T>(string envId, Func<string, string, Task<T>> action, CancellationToken ct = default);

    /// <summary>
    /// Tests if the provided credentials can authenticate by querying the UserNames IDO.
    /// </summary>
    /// <returns>True if authentication successful, false otherwise.</returns>
    Task<bool> CanAuthenticateAsync(string envId, CancellationToken ct = default);

    /// <summary>
    /// Same check as <see cref="CanAuthenticateAsync"/>, but says why when it fails: wrong credentials, the
    /// server not answering the token request, or the token not being accepted.
    /// </summary>
    Task<AuthCheckResult> CheckAuthenticationAsync(string envId, CancellationToken ct = default);

    /// <summary>
    /// Verifies that the environment's configured query IDO actually works, end to end, by running a
    /// harmless <c>SELECT 1</c> through its <c>ExecuteQuery</c> method. Returns a specific, actionable
    /// problem when it doesn't (IDO name not found, method missing or misconfigured, no data returned).
    /// </summary>
    Task<IdoValidationResult> ValidateQueryIdoAsync(string envId, CancellationToken ct = default);

}
/// <summary>Outcome of <see cref="IEnvironmentSessionManager.CheckAuthenticationAsync"/>.</summary>
/// <param name="Ok">True when a token was obtained and SyteLine accepted it.</param>
/// <param name="Problem">When not OK, why. Never contains the password.</param>
public sealed record AuthCheckResult(bool Ok, string? Problem)
{
    public static AuthCheckResult Success() => new(true, null);
    public static AuthCheckResult Failure(string problem) => new(false, problem);
}

/// <summary>Outcome of <see cref="IEnvironmentSessionManager.ValidateQueryIdoAsync"/>.</summary>
/// <param name="Ok">True when a test query ran through the IDO and returned data.</param>
/// <param name="Problem">When not OK, a message that says what to fix.</param>
/// <param name="Warning">When OK but worth knowing (for example the IDO is an older version), a message for the user. Never blocks.</param>
public sealed record IdoValidationResult(bool Ok, string? Problem, string? Warning = null)
{
    public static IdoValidationResult Success() => new(true, null);
    public static IdoValidationResult SuccessWithWarning(string warning) => new(true, null, warning);
    public static IdoValidationResult Failure(string problem) => new(false, problem);
}
