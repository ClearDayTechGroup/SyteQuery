namespace SyteQuery.Features.Common.Services;

/// <summary>
/// Base service class that provides the "current user" context. In this single-user
/// desktop app there's no login, so this always resolves to the one seeded local user
/// (see LocalUser) rather than reading an authenticated principal off HttpContext.
/// Kept as a base class (instead of touching every call site) so IEnvironmentSessionManager,
/// DatabaseQueryHistoryService and DatabaseQuerySnippetService didn't need any changes
/// beyond this.
/// </summary>
public abstract class BaseAuthenticatedService
{
    /// <summary>
    /// Gets the current user's ID. Always the local user in this single-user app.
    /// </summary>
    protected string? GetCurrentUserId() => LocalUser.Id;

    /// <summary>
    /// Gets the current user's ID. Always the local user in this single-user app.
    /// </summary>
    protected string GetCurrentUserIdRequired() => LocalUser.Id;
}
