using Newtonsoft.Json;
using System.Collections.Concurrent;
using SyteQuery.Features.Database.Entities;
using SyteQuery.Features.Environments.Repositories;
using SyteQuery.Features.DataExport.Services;
using SyteQuery.Features.DatabaseQuery.Services;
using SyteQuery.Features.QueryEditor.Models;
using SyteQuery.Features.QueryEditor.Services;
using SyteQuery.Features.Common.Services;

namespace SyteQuery.Features.Environments.Services;

public sealed class DatabaseEnvironmentSessionManager : BaseAuthenticatedService, IEnvironmentSessionManager, IDisposable
{
    // The method every query IDO must expose (the IDO's *name* is per-environment - see EnvProfile.IdoName).
    private const string IdoMethod = "ExecuteQuery";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IIdoHttpClient _idoHttpClient;
    private readonly IdoVersionRegistry _idoVersions;
    private readonly ConcurrentDictionary<string, EnvProfile> _profilesCache = new();
    private readonly ConcurrentDictionary<string, string> _tokenCache = new(); // key: url|config|user
    private string? _cachedUserId;

    public event Action? ProfilesChanged;

    public DatabaseEnvironmentSessionManager(
        IServiceScopeFactory scopeFactory,
        IIdoHttpClient idoHttpClient,
        IdoVersionRegistry idoVersions)
    {
        _scopeFactory = scopeFactory;
        _idoHttpClient = idoHttpClient;
        _idoVersions = idoVersions;
    }

    public IReadOnlyList<EnvProfile> Profiles
    {
        get
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return new List<EnvProfile>();

            // If user changed, clear cache
            if (_cachedUserId != userId)
            {
                _profilesCache.Clear();
                _cachedUserId = userId;
            }

            return _profilesCache.Values.ToList();
        }
    }

    public async Task InitializeAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserEnvironmentRepository>();
        var environments = await repository.GetByUserIdAsync(userId);

        _profilesCache.Clear();
        // Mark the cache as belonging to this user - otherwise the first read of Profiles
        // sees _cachedUserId (null) != userId, treats it as a user change, and wipes
        // everything just loaded. Environments were saved to the DB fine; they just
        // vanished from memory right after every startup.
        _cachedUserId = userId;
        foreach (var env in environments)
        {
            var profile = ToEnvProfile(repository, env);
            _profilesCache.TryAdd(profile.Id, profile);
        }

        // Notify that profiles have been loaded
        if (_profilesCache.Count > 0)
        {
            ProfilesChanged?.Invoke();
        }
    }

    private EnvProfile ToEnvProfile(IUserEnvironmentRepository repository, UserEnvironmentDb env)
    {
        return new EnvProfile
        {
            Id = env.EnvironmentId.ToString(),
            Name = env.Name,
            Url = env.Url,
            Config = env.ConfigName,
            User = env.Username,
            IdoName = env.IdoName,
            TokenMode = Enum.IsDefined(typeof(IdoTokenMode), env.TokenMode) ? (IdoTokenMode)env.TokenMode : IdoTokenMode.Auto,
            Password = repository.DecryptPassword(env.EncryptedPassword),
            IsConnected = false
        };
    }

    // ------------------------------------------------------------
    // Profile Management
    // ------------------------------------------------------------

    public async Task<EnvProfile> AddAsync(EnvProfile profile)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("User not authenticated");

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserEnvironmentRepository>();

        var envDb = new UserEnvironmentDb
        {
            UserId = userId,
            EnvironmentId = Guid.Parse(profile.Id),
            Name = profile.Name,
            Url = profile.Url,
            ConfigName = profile.Config,
            IdoName = profile.IdoName,
            TokenMode = (int)profile.TokenMode,
            Username = profile.User,
            EncryptedPassword = repository.EncryptPassword(profile.Password)
        };

        await repository.AddAsync(envDb);

        // Add to cache
        _profilesCache.TryAdd(profile.Id, profile);

        ProfilesChanged?.Invoke();

        return profile;
    }

    public async Task UpdateAsync(EnvProfile updatedProfile)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("User not authenticated");

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserEnvironmentRepository>();

        var existing = await repository.GetByIdAsync(userId, Guid.Parse(updatedProfile.Id));
        if (existing == null)
            throw new InvalidOperationException($"Profile '{updatedProfile.Id}' not found");

        existing.Name = updatedProfile.Name;
        existing.Url = updatedProfile.Url;
        existing.ConfigName = updatedProfile.Config;
        existing.IdoName = updatedProfile.IdoName;
        existing.TokenMode = (int)updatedProfile.TokenMode;
        existing.Username = updatedProfile.User;
        existing.EncryptedPassword = repository.EncryptPassword(updatedProfile.Password);

        await repository.UpdateAsync(existing);

        // Update cache
        _profilesCache[updatedProfile.Id] = updatedProfile;

        // Credentials may have changed - drop any cached token for the old values
        InvalidateTokenCache(updatedProfile);

        ProfilesChanged?.Invoke();
    }

    public async Task RemoveAsync(string envId)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("User not authenticated");

        await DisconnectAsync(envId);

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserEnvironmentRepository>();
        await repository.DeleteAsync(userId, Guid.Parse(envId));

        // Remove from cache
        _profilesCache.TryRemove(envId, out _);

        ProfilesChanged?.Invoke();
    }

    // ------------------------------------------------------------
    // Session Management
    // ------------------------------------------------------------

    public Task ConnectAsync(string envId, CancellationToken ct = default)
    {
        // REST v2 is stateless (token-based, not session-based) - nothing to open.
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(string envId)
    {
        // Drop any cached token for this environment, forcing a fresh GetSecurityToken
        // call next time it's used (e.g. after a credential change or on user request).
        if (_profilesCache.TryGetValue(envId, out var profile))
        {
            InvalidateTokenCache(profile);
        }
        return Task.CompletedTask;
    }

    private void InvalidateTokenCache(EnvProfile profile)
        => _tokenCache.TryRemove(TokenCacheKey(profile), out _);

    private static string TokenCacheKey(EnvProfile profile)
        => $"{profile.Url}|{profile.Config}|{profile.User}";

    // ------------------------------------------------------------
    // REST Session Access
    // ------------------------------------------------------------

    public async Task<T> UseSessionAsync<T>(
        string envId,
        Func<string, string, Task<T>> action,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("User not authenticated");

        // Try to get profile from cache first
        if (!_profilesCache.TryGetValue(envId, out var profile))
        {
            // If not in cache, fetch from database
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IUserEnvironmentRepository>();
            var envDb = await repository.GetByIdAsync(userId, Guid.Parse(envId));

            if (envDb == null)
                throw new InvalidOperationException("Environment not found");

            profile = ToEnvProfile(repository, envDb);

            // Add to cache for future use
            _profilesCache.TryAdd(envId, profile);
        }

        // Validate profile
        ValidateProfile(profile);

        var token = await GetOrFetchTokenAsync(profile, ct);

        return await action(profile.Url, token);
    }

    private async Task<string> GetOrFetchTokenAsync(EnvProfile profile, CancellationToken ct)
    {
        var key = TokenCacheKey(profile);
        if (_tokenCache.TryGetValue(key, out var cachedToken))
            return cachedToken;

        var result = await _idoHttpClient.GetTokenAsync(profile.Url, profile.Config, profile.User, profile.Password, profile.TokenMode, ct);
        if (!result.Success || string.IsNullOrEmpty(result.Token))
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.Message)
                    ? "Failed to authenticate with the SyteLine environment."
                    : result.Message);
        }

        _tokenCache[key] = result.Token;
        return result.Token;
    }

    private static void ValidateProfile(EnvProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Url))
            throw new ArgumentException("URL is required", nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.User))
            throw new ArgumentException("Username is required", nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.Config))
            throw new ArgumentException("Configuration is required", nameof(profile));
    }

    // ------------------------------------------------------------
    // Query Execution
    // ------------------------------------------------------------

    public async Task<QueryResult> ExecuteAsync(string envId, string cmd, CancellationToken ct = default)
    {
        try
        {
            var idoName = GetIdoName(envId);
            return await UseSessionAsync(envId, async (url, token) =>
            {
                var response = await _idoHttpClient.InvokeAsync(url, token, idoName, IdoMethod, new[] { cmd, null, null }, ct);

                if (!response.Success)
                {
                    var errorMessage = response.Message ?? string.Empty;
                    if (errorMessage.Contains("not a valid IDO name", StringComparison.OrdinalIgnoreCase))
                    {
                        // The configured IDO doesn't exist (wrong name, or it was deleted in SyteLine)
                        return QueryResult.Failure(
                            $"The IDO '{idoName}' was not found in this environment. Check the IDO name under " +
                            "Tools > Environments > Edit, and that the IDO exists in SyteLine. " + SetupGuideHint);
                    }

                    return QueryResult.Failure(
                        string.IsNullOrWhiteSpace(errorMessage) ? "Query execution failed." : errorMessage);
                }

                var dataPayload = response.Parameters.Count > 1 ? response.Parameters[1] ?? string.Empty : string.Empty;
                var message = response.Parameters.Count > 2 ? response.Parameters[2] ?? string.Empty : string.Empty;

                return QueryResult.Success(dataPayload, message);
            }, ct);
        }
        catch (Exception ex)
        {
            return QueryResult.Failure($"Query execution failed: {ex.Message}");
        }
    }

    // ------------------------------------------------------------
    // Metadata Queries
    // ------------------------------------------------------------

    public async Task<IReadOnlyList<string>> GetTablesAsync(string envId, CancellationToken ct = default)
        => await GetMetadataAsync(envId, MetadataType.Tables, ct);

    public async Task<IReadOnlyList<string>> GetViewsAsync(string envId, CancellationToken ct = default)
        => await GetMetadataAsync(envId, MetadataType.Views, ct);

    public async Task<IReadOnlyList<string>> GetProcsAsync(string envId, CancellationToken ct = default)
        => await GetMetadataAsync(envId, MetadataType.Procedures, ct);

    private async Task<IReadOnlyList<string>> GetMetadataAsync(
        string envId,
        MetadataType type,
        CancellationToken ct)
    {
        // Query database
        var sql = GetMetadataQuery(type);
        var result = await ExecuteAsync(envId, sql, ct);

        if (!result.Ok)
            throw new InvalidOperationException($"Metadata query failed: {result.Message}");

        // Parse results
        return ExtractColumnValues(result.DataBase64, "Name");
    }

    private static string GetMetadataQuery(MetadataType type) => type switch
    {
        MetadataType.Tables => @"
            SELECT s.name + '.' + t.name AS [Name]
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            ORDER BY 1",

        MetadataType.Views => @"
            SELECT s.name + '.' + v.name AS [Name]
            FROM sys.views v
            JOIN sys.schemas s ON s.schema_id = v.schema_id
            ORDER BY 1",

        MetadataType.Procedures => @"
            SELECT s.name + '.' + p.name AS [Name]
            FROM sys.procedures p
            JOIN sys.schemas s ON s.schema_id = p.schema_id
            ORDER BY 1",

        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static List<string> ExtractColumnValues(string? payload, string columnName)
    {
        // Metadata queries return a single result set: take the first.
        if (!IdoResultParser.TryParse(payload, out var parsed, out _) || parsed!.ResultSets.Count == 0)
            return new List<string>();

        return parsed.ResultSets[0].Rows
            .Select(r => r.TryGetValue(columnName, out var val) ? val?.ToString() : null)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .ToList();
    }

    // ------------------------------------------------------------
    // Environment Validation
    // ------------------------------------------------------------

    public async Task<bool> CanAuthenticateAsync(string envId, CancellationToken ct = default)
        => (await CheckAuthenticationAsync(envId, ct)).Ok;

    public async Task<AuthCheckResult> CheckAuthenticationAsync(string envId, CancellationToken ct = default)
    {
        try
        {
            return await UseSessionAsync(envId, async (url, token) =>
            {
                var result = await _idoHttpClient.LoadCollectionAsync(url, token, "SLItems", properties: "Item", recordCap: 1, ct: ct);
                return result.Success
                    ? AuthCheckResult.Success()
                    : AuthCheckResult.Failure(
                        "SyteLine gave a security token but wouldn't read from it: " +
                        (string.IsNullOrWhiteSpace(result.Message) ? "no details were returned." : result.Message));
            }, ct);
        }
        catch (Exception ex)
        {
            // Asking for the token failing lands here, with the reason (which token requests were tried and what came back).
            return AuthCheckResult.Failure(ex.Message);
        }
    }

    // ------------------------------------------------------------
    // Query IDO validation
    // ------------------------------------------------------------

    private string GetIdoName(string envId)
    {
        if (_profilesCache.TryGetValue(envId, out var profile) && !string.IsNullOrWhiteSpace(profile.IdoName))
            return profile.IdoName;

        throw new InvalidOperationException(
            "No IDO name is configured for this environment. Set it under Tools > Environments > Edit.");
    }

    public async Task<IdoValidationResult> ValidateQueryIdoAsync(string envId, CancellationToken ct = default)
    {
        string idoName;
        try
        {
            idoName = GetIdoName(envId);
        }
        catch (Exception ex)
        {
            return IdoValidationResult.Failure(ex.Message);
        }

        try
        {
            return await UseSessionAsync(envId, async (url, token) =>
            {
                // Run a real query through the real method: "does an IDO with this name exist?" isn't enough
                // now that users create the IDO by hand - it can exist and still not be bound to the assembly,
                // or be missing the method, and each of those fails differently.
                var response = await _idoHttpClient.InvokeAsync(
                    url, token, idoName, IdoMethod, new[] { "SELECT 1 AS ok", null, null }, ct);

                if (!response.Success)
                    return IdoValidationResult.Failure(DescribeInvokeFailure(idoName, response.Message));

                var data = response.Parameters.Count > 1 ? response.Parameters[1] ?? string.Empty : string.Empty;
                var message = response.Parameters.Count > 2 ? response.Parameters[2] ?? string.Empty : string.Empty;

                if (string.IsNullOrWhiteSpace(data))
                {
                    return QueryErrorDetector.LooksLikeError(message)
                        ? IdoValidationResult.Failure($"The IDO '{idoName}' answered with an error instead of data: {message}")
                        : IdoValidationResult.Failure(
                            $"The IDO '{idoName}' ran but returned no data. ExecuteQuery must return the result rows as JSON in its " +
                            $"second (Output) parameter. {SetupGuideHint}");
                }

                if (!IdoResultParser.TryParse(data, out var parsed, out var problem))
                {
                    var json = DataEncoder.TryDecodeBase64ToUtf8(data) ?? data;
                    var preview = json.Length > 120 ? json[..120] + "..." : json;
                    return IdoValidationResult.Failure(
                        $"The IDO '{idoName}' returned data SyteQuery couldn't read: {problem} It returned: {preview}");
                }

                var returnedOk = parsed!.ResultSets.Count > 0 &&
                                 parsed.ResultSets[0].Rows.Any(r => r.Keys.Any(k => k.Equals("ok", StringComparison.OrdinalIgnoreCase)));
                if (!returnedOk)
                {
                    var json = DataEncoder.TryDecodeBase64ToUtf8(data) ?? data;
                    var preview = json.Length > 120 ? json[..120] + "..." : json;
                    return IdoValidationResult.Failure(
                        $"The IDO '{idoName}' returned data, but not the rows SyteQuery expected (a result with an \"ok\" column). " +
                        $"It returned: {preview}");
                }

                _idoVersions.Record(envId, parsed.Version);

                if (parsed.Version < IdoVersion.MultipleResultSets)
                {
                    return IdoValidationResult.SuccessWithWarning(
                        $"The IDO '{idoName}' works, but it is an older version: it returns only the first result set of a command. " +
                        $"Ask your SyteLine administrator to install the updated IDO to run scripts with several SELECT statements. {SetupGuideHint}");
                }

                return IdoValidationResult.Success();
            }, ct);
        }
        catch (Exception ex)
        {
            return IdoValidationResult.Failure($"Couldn't call SyteLine to check the IDO: {ex.Message}");
        }
    }

    private const string SetupGuideHint = "See the IDO setup guide (SyteQuery.IDO/README.md in the SyteQuery repository).";

    private static string DescribeInvokeFailure(string idoName, string? message)
    {
        message = string.IsNullOrWhiteSpace(message) ? "no details were returned" : message.Trim();

        if (message.Contains("not a valid IDO name", StringComparison.OrdinalIgnoreCase))
        {
            return $"SyteLine doesn't know an IDO named '{idoName}'. Check that the name matches the IDO you created exactly " +
                   $"(and that you saved it and reloaded the IDO metadata). {SetupGuideHint}";
        }

        return $"SyteLine rejected the call to {idoName}.{IdoMethod}: {message}. Check that the IDO has a method named " +
               $"{IdoMethod} with the three parameters from the setup guide, and that the assembly is bound to the IDO. {SetupGuideHint}";
    }

    // ------------------------------------------------------------
    // Cleanup
    // ------------------------------------------------------------

    public void Dispose()
    {
        // REST v2 is stateless - nothing to dispose.
    }
}

// ------------------------------------------------------------
// Supporting Types
// ------------------------------------------------------------

internal enum MetadataType
{
    Tables,
    Views,
    Procedures
}
