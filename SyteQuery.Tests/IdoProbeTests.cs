using SyteQuery.Features.DatabaseQuery.Services;
using SyteQuery.Features.Environments.Services;
using SyteQuery.Features.QueryEditor.Models;
using Xunit;
using static SyteQuery.Tests.Payloads;

namespace SyteQuery.Tests;

/// <summary>
/// Asking the IDO what it is. The app used to remember the version from the last response and warn from memory, so
/// after the IDO was upgraded it kept warning until a query happened to refresh the memory. The probe asks the IDO
/// itself, so the answer reflects what SyteLine is running now.
/// </summary>
public class IdoProbeTests
{
    private const string TwoSets = "{\"version\":2,\"resultSets\":[" +
        "{\"columns\":[\"probe_a\"],\"rows\":[{\"probe_a\":1}]},{\"columns\":[\"probe_b\"],\"rows\":[{\"probe_b\":2}]}]}";
    private const string OldIdoAnswer = "[{\"probe_a\":1}]";   // the original IDO: first result set only, bare list

    private static (IdoQueryService service, IdoVersionRegistry versions, List<string> commands) Service(Func<string, QueryResult> onExecute)
    {
        var versions = new IdoVersionRegistry();
        var commands = new List<string>();
        var env = EnvironmentManagerProxy.Create(cmd => { commands.Add(cmd); return onExecute(cmd); });
        return (new IdoQueryService(env, versions), versions, commands);
    }

    [Fact]
    public async Task The_current_IDO_is_recognised_by_its_answer()
    {
        var (service, versions, commands) = Service(_ => QueryResult.Success(Base64(TwoSets), ""));

        var probe = await service.ProbeIdoAsync("env1");

        Assert.Equal(IdoStatus.Current, probe.Status);
        Assert.Equal(2, probe.FormatVersion);
        Assert.Equal(IdoQueryService.ProbeCommand, Assert.Single(commands));
        Assert.False(versions.IsOutdated("env1"));
    }

    [Fact]
    public async Task The_older_IDO_is_recognised_and_the_advice_covers_a_cached_assembly()
    {
        var (service, versions, _) = Service(_ => QueryResult.Success(Base64(OldIdoAnswer), ""));

        var probe = await service.ProbeIdoAsync("env1");

        Assert.Equal(IdoStatus.Older, probe.Status);
        Assert.Equal(1, probe.FormatVersion);
        Assert.True(versions.IsOutdated("env1"));
        Assert.Contains("still running the old one", probe.Summary);
        Assert.Contains("reload", probe.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Current_code_that_returns_only_one_result_set_is_called_out_separately()
    {
        // The new IDO is running (new format) but SyteLine's database layer didn't pass the second result set on.
        var oneSet = "{\"version\":2,\"resultSets\":[{\"columns\":[\"probe_a\"],\"rows\":[{\"probe_a\":1}]}]}";
        var (service, versions, _) = Service(_ => QueryResult.Success(Base64(oneSet), ""));

        var probe = await service.ProbeIdoAsync("env1");

        Assert.Equal(IdoStatus.CurrentButOneResultSet, probe.Status);
        Assert.Equal(2, probe.FormatVersion);
        Assert.False(versions.IsOutdated("env1"));   // the code is current; this is not the "install the new IDO" case
        Assert.Contains("report", probe.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_unreachable_IDO_says_so_and_changes_nothing()
    {
        var (service, versions, _) = Service(_ => QueryResult.Failure("The IDO 'x' was not found."));

        var probe = await service.ProbeIdoAsync("env1");

        Assert.Equal(IdoStatus.Unreachable, probe.Status);
        Assert.Contains("was not found", probe.Summary);
        Assert.Null(versions.Get("env1"));
    }

    // ---- the bug: remembered "older", IDO since upgraded ----

    [Fact]
    public async Task A_remembered_older_version_is_corrected_when_the_IDO_has_been_upgraded()
    {
        // The IDO answers with the old format first (this is what the app remembers)...
        var current = Base64(OldIdoAnswer);
        var (service, versions, _) = Service(_ => QueryResult.Success(current, ""));
        await service.ExecuteAsync("env1", "SELECT 1");
        Assert.True(versions.IsOutdated("env1"));

        // ...then an administrator installs the new assembly and SyteLine starts running it.
        current = Base64(TwoSets);

        var probe = await service.ProbeIdoAsync("env1");

        Assert.Equal(IdoStatus.Current, probe.Status);
        Assert.False(versions.IsOutdated("env1"));   // what the app remembers is now right
        Assert.Equal(2, versions.Get("env1"));
    }

    [Fact]
    public async Task A_remembered_older_version_stays_when_SyteLine_is_still_running_the_old_assembly()
    {
        var (service, versions, _) = Service(_ => QueryResult.Success(Base64(OldIdoAnswer), ""));
        await service.ExecuteAsync("env1", "SELECT 1");

        var probe = await service.ProbeIdoAsync("env1");

        Assert.Equal(IdoStatus.Older, probe.Status);
        Assert.True(versions.IsOutdated("env1"));
    }
}
