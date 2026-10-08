using SyteQuery.Features.DatabaseQuery.Services;
using Xunit;
using static SyteQuery.Tests.Payloads;

namespace SyteQuery.Tests;

public class IdoResultParserTests
{
    private static IdoPayload Parse(string payload)
    {
        Assert.True(IdoResultParser.TryParse(payload, out var result, out var problem), problem);
        return result!;
    }

    // ---- version 1: the original IDO sent one bare list of rows ----

    [Fact]
    public void Version1_base64_list_is_one_result_set()
    {
        var p = Parse(Base64("[{\"a\":1,\"b\":\"x\"},{\"a\":2,\"b\":null}]"));

        Assert.Equal(1, p.Version);
        var set = Assert.Single(p.ResultSets);
        Assert.Equal(2, set.RowCount);
        Assert.Equal(new[] { "a", "b" }, set.Columns);
        Assert.IsType<long>(set.Rows[0]["a"]);
        Assert.Null(set.Rows[1]["b"]);
        Assert.Null(p.Error);
    }

    [Fact]
    public void Version1_plain_json_list_works_too()
    {
        var p = Parse("[{\"a\":1}]");
        Assert.Equal(1, p.ResultSets[0].RowCount);
    }

    [Fact]
    public void Version1_empty_list_is_one_empty_set_without_columns()
    {
        var set = Assert.Single(Parse(Base64("[]")).ResultSets);
        Assert.Equal(0, set.RowCount);
        Assert.Empty(set.Columns);
    }

    // ---- version 2 ----

    [Fact]
    public void Version2_reads_every_result_set_with_its_columns()
    {
        var p = Parse(Base64("{\"version\":2,\"resultSets\":[" +
                            "{\"columns\":[\"x\",\"y\"],\"rows\":[{\"x\":1,\"y\":\"a\"}]}," +
                            "{\"columns\":[\"z\"],\"rows\":[]}]}"));

        Assert.Equal(2, p.Version);
        Assert.Equal(2, p.ResultSets.Count);
        Assert.Equal(new[] { "z" }, p.ResultSets[1].Columns);   // empty set keeps its headers
        Assert.Equal(0, p.ResultSets[1].RowCount);
    }

    [Fact]
    public void Version2_with_no_result_sets_is_valid()
    {
        Assert.Empty(Parse(Base64("{\"version\":2,\"resultSets\":[]}")).ResultSets);
    }

    [Fact]
    public void Version2_partial_failure_keeps_the_sets_and_the_error()
    {
        var p = Parse(Base64("{\"version\":2,\"resultSets\":[{\"columns\":[\"a\"],\"rows\":[{\"a\":1}]}],\"error\":\"boom\"}"));

        Assert.Equal("boom", p.Error);
        Assert.Single(p.ResultSets);
    }

    [Fact]
    public void A_missing_version_number_is_taken_as_current()
    {
        Assert.Equal(IdoResultParser.LatestVersion, Parse(Base64("{\"resultSets\":[]}")).Version);
    }

    [Fact]
    public void A_newer_version_than_the_app_knows_is_refused_with_advice()
    {
        var ok = IdoResultParser.TryParse(Base64("{\"version\":99,\"resultSets\":[]}"), out var result, out var problem);

        Assert.False(ok);
        Assert.Null(result);
        Assert.Contains("Update SyteQuery", problem);
    }

    // ---- values ----

    [Fact]
    public void Values_keep_their_types()
    {
        var row = Parse(Base64("[{\"d\":\"2026-10-08T13:45:10\",\"n\":1.5,\"b\":true,\"o\":{\"k\":1}}]")).ResultSets[0].Rows[0];

        Assert.IsType<DateTime>(row["d"]);
        Assert.IsType<double>(row["n"]);
        Assert.IsType<bool>(row["b"]);
        Assert.IsAssignableFrom<Newtonsoft.Json.Linq.JToken>(row["o"]);
    }

    // ---- bad input ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello world")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"foo\":1}")]
    public void Unreadable_payloads_are_rejected_with_a_message(string payload)
    {
        var ok = IdoResultParser.TryParse(payload, out var result, out var problem);

        Assert.False(ok);
        Assert.Null(result);
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }
}
