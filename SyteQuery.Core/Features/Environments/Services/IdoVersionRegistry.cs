using System.Collections.Concurrent;

namespace SyteQuery.Features.Environments.Services;

/// <summary>
/// Remembers, per environment, which output format version its query IDO answered with (for this run
/// of the app). The version comes free with every response - nothing extra is called - and lets the
/// app say "this environment's IDO is out of date" at the moment it matters, instead of silently
/// showing only the first of several result sets.
/// </summary>
public sealed class IdoVersionRegistry
{
    private readonly ConcurrentDictionary<string, int> _versions = new();

    /// <summary>The version last seen for the environment, or null if it hasn't answered yet.</summary>
    public int? Get(string envId) => _versions.TryGetValue(envId, out var v) ? v : null;

    public void Record(string envId, int version) => _versions[envId] = version;

    /// <summary>True when the environment's IDO is known to predate multiple result sets.</summary>
    public bool IsOutdated(string envId) => Get(envId) is { } v && v < IdoVersion.MultipleResultSets;
}

/// <summary>The IDO output format versions SyteQuery knows about.</summary>
public static class IdoVersion
{
    /// <summary>The original IDO: the first result set only, as a bare JSON list.</summary>
    public const int SingleResultSet = 1;

    /// <summary>Returns every result set (and a partial-failure error).</summary>
    public const int MultipleResultSets = 2;
}
