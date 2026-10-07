namespace SyteQuery.Features.Common;

/// <summary>
/// Single-user desktop app: there's no login screen, so every "per user" database row
/// (environments, query history, snippets, scheduled jobs, notification connectors)
/// belongs to this one well-known local user. Seeded once at startup by SeedDatabase.
/// </summary>
public static class LocalUser
{
    public const string Id = "local-user";
}
