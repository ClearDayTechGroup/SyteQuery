using SyteQuery.Features.DatabaseQuery.Services;
using SyteQuery.Features.Environments.Services;
using SyteQuery.Features.QueryEditor.Models;
using Xunit;
using static SyteQuery.Tests.Payloads;

namespace SyteQuery.Tests;

public class IdoQueryServiceTests
{
    private static (IdoQueryService service, IdoVersionRegistry versions) Service(Func<string, QueryResult> onExecute)
    {
        var versions = new IdoVersionRegistry();
        return (new IdoQueryService(EnvironmentManagerProxy.Create(onExecute), versions), versions);
    }

    private static async Task<(QueryExecutionResult result, IdoVersionRegistry versions)> Run(string payload, string message = "")
    {
        var (service, versions) = Service(_ => QueryResult.Success(payload, message));
        return (await service.ExecuteAsync("env1", "SELECT 1"), versions);
    }

    [Fact]
    public async Task Old_IDO_answer_works_and_marks_the_environment_outdated()
    {
        var (r, versions) = await Run(Base64("[{\"a\":1}]"));

        Assert.True(r.Success);
        Assert.Equal(1, r.IdoVersion);
        Assert.Equal(1, versions.Get("env1"));
        Assert.True(versions.IsOutdated("env1"));
        Assert.Equal(1, r.RowCount);
        Assert.Single(r.ResultSets);
    }

    [Fact]
    public async Task New_IDO_answer_gives_every_result_set()
    {
        var (r, versions) = await Run(Base64("{\"version\":2,\"resultSets\":[" +
            "{\"columns\":[\"a\"],\"rows\":[{\"a\":1}]}," +
            "{\"columns\":[\"b\"],\"rows\":[{\"b\":2},{\"b\":3}]}]}"));

        Assert.True(r.Success);
        Assert.Equal(2, r.ResultSets.Count);
        Assert.Equal(1, r.RowCount);          // Rows / RowCount stay the first set
        Assert.Equal(3, r.TotalRowCount);
        Assert.Equal(2, versions.Get("env1"));
        Assert.False(versions.IsOutdated("env1"));
    }

    [Fact]
    public async Task A_failure_part_way_is_not_success_but_keeps_what_came_back()
    {
        var (r, _) = await Run(
            Base64("{\"version\":2,\"resultSets\":[{\"columns\":[\"a\"],\"rows\":[{\"a\":1}]}],\"error\":\"Divide by zero error encountered.\"}"),
            "Divide by zero error encountered.");

        Assert.False(r.Success);
        Assert.Contains("Divide by zero", r.Message);
        Assert.Single(r.ResultSets);
        Assert.Equal(1, r.RowCount);
    }

    [Fact]
    public async Task Empty_data_with_an_error_looking_message_is_a_failure()
    {
        var (r, _) = await Run("", "Invalid object name 'nope'.");

        Assert.False(r.Success);
        Assert.Contains("Invalid object name", r.Message);
    }

    [Fact]
    public async Task Empty_data_and_no_message_is_success()
    {
        var (r, _) = await Run("");

        Assert.True(r.Success);
        Assert.Equal("No data returned", r.Message);
    }

    [Fact]
    public async Task A_command_with_no_SELECT_succeeds_with_no_result_sets()
    {
        var (r, _) = await Run(Base64("{\"version\":2,\"resultSets\":[]}"));

        Assert.True(r.Success);
        Assert.Null(r.Rows);
        Assert.Empty(r.ResultSets);
        Assert.Equal("Command complete", r.Message);
    }

    [Fact]
    public async Task A_newer_IDO_than_the_app_is_refused_and_not_recorded()
    {
        var (r, versions) = await Run(Base64("{\"version\":9,\"resultSets\":[]}"));

        Assert.False(r.Success);
        Assert.Contains("Update SyteQuery", r.Message);
        Assert.Null(versions.Get("env1"));
    }

    // ---- scripts split at GO ----

    [Fact]
    public async Task Batches_are_run_in_order_and_their_result_sets_combined()
    {
        var calls = new List<string>();
        var payloads = new Queue<string>(new[] { Base64(Set("a", 1)), Base64(Set("b", 2)), Base64(Set("c", 3)) });
        var (service, _) = Service(sql => { calls.Add(sql); return QueryResult.Success(payloads.Dequeue(), ""); });

        var r = await service.ExecuteBatchesAsync("env1", new[] { "select 1", "select 2", "select 3" });

        Assert.Equal(new[] { "select 1", "select 2", "select 3" }, calls);
        Assert.True(r.Success);
        Assert.Equal(new[] { "a", "b", "c" }, r.ResultSets.Select(s => s.Columns[0]));
        Assert.Equal(6, r.TotalRowCount);
    }

    [Fact]
    public async Task A_failing_batch_stops_the_run_and_keeps_the_earlier_results()
    {
        var calls = 0;
        var payloads = new Queue<string>(new[]
        {
            Base64(Set("a", 1)),
            Base64("{\"version\":2,\"resultSets\":[],\"error\":\"Invalid object name 'x'.\"}"),
            Base64(Set("c", 3))
        });
        var (service, _) = Service(_ => { calls++; return QueryResult.Success(payloads.Dequeue(), ""); });

        var r = await service.ExecuteBatchesAsync("env1", new[] { "select 1", "select * from x", "select 3" });

        Assert.False(r.Success);
        Assert.Equal(2, calls);                        // batch 3 never ran
        Assert.Single(r.ResultSets);
        Assert.StartsWith("Batch 2 of 3 failed:", r.Message);
        Assert.Contains("Invalid object name", r.Message);
    }

    [Fact]
    public async Task A_single_batch_is_passed_straight_through()
    {
        var (service, _) = Service(_ => QueryResult.Success(Base64(Set("only", 2)), ""));

        var r = await service.ExecuteBatchesAsync("env1", new[] { "select 1" });

        Assert.True(r.Success);
        Assert.Equal(2, r.RowCount);
    }

    [Fact]
    public async Task No_batches_is_an_error()
    {
        var (service, _) = Service(_ => QueryResult.Success("", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteBatchesAsync("env1", Array.Empty<string>()));
    }
}

public class IdoVersionRegistryTests
{
    [Fact]
    public void Unknown_environment_is_not_called_outdated()
    {
        var registry = new IdoVersionRegistry();

        Assert.Null(registry.Get("env"));
        Assert.False(registry.IsOutdated("env"));
    }

    [Fact]
    public void Remembers_the_latest_version_seen_per_environment()
    {
        var registry = new IdoVersionRegistry();
        registry.Record("a", IdoVersion.SingleResultSet);
        registry.Record("b", IdoVersion.MultipleResultSets);

        Assert.True(registry.IsOutdated("a"));
        Assert.False(registry.IsOutdated("b"));

        registry.Record("a", IdoVersion.MultipleResultSets);   // the admin updated the IDO
        Assert.False(registry.IsOutdated("a"));
    }
}
