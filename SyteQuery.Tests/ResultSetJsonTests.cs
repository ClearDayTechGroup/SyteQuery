using System.Data;
using System.Reflection;
using Newtonsoft.Json.Linq;
using QueryTool;
using SyteQuery.Features.DatabaseQuery.Services;
using Xunit;

namespace SyteQuery.Tests;

/// <summary>
/// The IDO's serializer (SyteQuery.IDO/ResultSetJson.cs, compiled into this project because the IDO project
/// itself needs Infor's assemblies). Uses DataSet readers, which move through result sets with NextResult like
/// a SqlDataReader does.
/// </summary>
public class ResultSetJsonTests
{
    private static DataTable Table(string name, string[] columns, params object?[][] rows)
    {
        var table = new DataTable(name);
        foreach (var column in columns)
            table.Columns.Add(column, typeof(object));
        foreach (var row in rows)
            table.Rows.Add(row.Select(v => v ?? DBNull.Value).ToArray());
        return table;
    }

    private static IDataReader Reader(params DataTable[] tables)
    {
        var set = new DataSet();
        foreach (var table in tables)
            set.Tables.Add(table);
        return set.CreateDataReader();
    }

    private static JObject Serialize(IDataReader reader, out string? error)
    {
        var json = ResultSetJson.Serialize(reader, out error);
        return JObject.Parse(json);   // must always be valid JSON
    }

    [Fact]
    public void One_result_set_is_version_2_with_columns_and_rows()
    {
        var json = Serialize(Reader(Table("t", new[] { "ok" }, new object?[] { 1 })), out var error);

        Assert.Null(error);
        Assert.Equal(2, (int)json["version"]!);
        var set = Assert.Single((JArray)json["resultSets"]!);
        Assert.Equal("ok", (string)set["columns"]![0]!);
        Assert.Equal(1, (int)set["rows"]![0]!["ok"]!);
    }

    [Fact]
    public void Every_result_set_is_returned_in_order()
    {
        var json = Serialize(Reader(
            Table("a", new[] { "item" }, new object?[] { "A" }),
            Table("b", new[] { "cust", "name" }, new object?[] { 1, "x" }, new object?[] { 2, "y" })), out _);

        var sets = (JArray)json["resultSets"]!;
        Assert.Equal(2, sets.Count);
        Assert.Equal("item", (string)sets[0]["columns"]![0]!);
        Assert.Equal(2, ((JArray)sets[1]["rows"]!).Count);
    }

    [Fact]
    public void An_empty_result_set_still_lists_its_columns()
    {
        var json = Serialize(Reader(Table("t", new[] { "name", "object_id" })), out _);

        var set = ((JArray)json["resultSets"]!).Single();
        Assert.Equal(new[] { "name", "object_id" }, set["columns"]!.Select(c => (string)c!));
        Assert.Empty((JArray)set["rows"]!);
    }

    [Fact]
    public void A_command_that_returns_no_result_set_gives_an_empty_list()
    {
        var json = Serialize(Reader(new DataTable("nothing")), out var error);   // no columns = no result set

        Assert.Null(error);
        Assert.Empty((JArray)json["resultSets"]!);
    }

    // ---- column names ----

    private static string[] Unique(params string[] names) =>
        ResultSetJson.UniqueColumnNames(NamesRecordProxy.Create(names), names.Length);

    [Fact]
    public void Duplicate_column_names_are_made_unique_ignoring_case()
    {
        Assert.Equal(new[] { "id", "id1", "ID2" }, Unique("id", "id", "ID"));
    }

    [Fact]
    public void Unnamed_columns_get_a_position_based_name()
    {
        Assert.Equal(new[] { "a", "Column2", "Column3" }, Unique("a", "", null!));
    }

    [Fact]
    public void A_generated_name_does_not_collide_with_a_real_one()
    {
        Assert.Equal(new[] { "id", "id1", "id11" }, Unique("id", "id", "id1"));
    }

    // ---- values ----

    [Fact]
    public void Values_are_written_as_json_values()
    {
        var guid = Guid.NewGuid();
        var json = Serialize(Reader(Table("t",
            new[] { "n", "b", "big", "dec", "dt", "bin", "g", "ts", "s" },
            new object?[]
            {
                null, true, long.MaxValue, 12.34m, new DateTime(2026, 10, 8, 13, 45, 10),
                new byte[] { 0xCA, 0xFE }, guid, TimeSpan.FromMinutes(90), "quote \" \\ slash Ünï 日本"
            })), out _);

        var row = (JObject)((JArray)json["resultSets"]![0]!["rows"]!)[0]!;
        Assert.Equal(JTokenType.Null, row["n"]!.Type);
        Assert.True((bool)row["b"]!);
        Assert.Equal(long.MaxValue, (long)row["big"]!);
        Assert.Equal(12.34m, (decimal)row["dec"]!);
        Assert.Equal("yv4=", (string)row["bin"]!);            // byte[] as base64
        Assert.Equal(guid, (Guid)row["g"]!);
        Assert.Equal("quote \" \\ slash Ünï 日本", (string)row["s"]!);
    }

    [Fact]
    public void Dates_are_written_as_iso_text()
    {
        var json = ResultSetJson.Serialize(Reader(Table("t", new[] { "dt" }, new object?[] { new DateTime(2026, 10, 8, 13, 45, 10) })));
        Assert.Contains("\"dt\":\"2026-10-08T13:45:10\"", json);
    }

    private sealed class OddType
    {
        public override string ToString() => "odd-value";
    }

    [Fact]
    public void A_type_json_has_no_notion_of_falls_back_to_its_text()
    {
        var json = Serialize(Reader(Table("t", new[] { "x" }, new object?[] { new OddType() })), out var error);

        Assert.Null(error);
        Assert.Equal("odd-value", (string)((JArray)json["resultSets"]![0]!["rows"]!)[0]!["x"]!);
    }

    // ---- failing part-way ----

    [Fact]
    public void A_failure_on_a_later_statement_keeps_the_earlier_result_sets_and_reports_the_error()
    {
        var reader = FailingReaderProxy.Wrap(
            Reader(Table("a", new[] { "first_ok" }, new object?[] { 1 }), Table("b", new[] { "never" })),
            failOnNextResult: 1);

        var json = Serialize(reader, out var error);

        Assert.Equal("Divide by zero error encountered.", error);
        Assert.Equal(error, (string)json["error"]!);
        var set = ((JArray)json["resultSets"]!).Single();
        Assert.Equal(1, (int)set["rows"]![0]!["first_ok"]!);
    }

    [Fact]
    public void A_failure_while_reading_rows_keeps_the_rows_read_so_far()
    {
        var reader = FailingReaderProxy.Wrap(
            Reader(Table("a", new[] { "v" }, new object?[] { 1 }, new object?[] { 2 }, new object?[] { 3 }, new object?[] { 4 })),
            failOnRead: 3);   // reads 1 and 2 succeed, the third fails

        var json = Serialize(reader, out var error);

        Assert.Equal("Arithmetic overflow error.", error);
        var rows = (JArray)((JArray)json["resultSets"]!).Single()["rows"]!;
        Assert.Equal(2, rows.Count);
    }

    // ---- what the app does with it ----

    [Fact]
    public void The_apps_parser_reads_what_the_IDO_writes()
    {
        var json = ResultSetJson.Serialize(Reader(
            Table("a", new[] { "item" }, new object?[] { "A100" }),
            Table("b", new[] { "cust" })));

        Assert.True(IdoResultParser.TryParse(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)), out var parsed, out var problem), problem);
        Assert.Equal(ResultSetJson.Version, parsed!.Version);
        Assert.Equal(2, parsed.ResultSets.Count);
        Assert.Equal("A100", parsed.ResultSets[0].Rows[0]["item"]);
        Assert.Equal(new[] { "cust" }, parsed.ResultSets[1].Columns);
        Assert.True(parsed.Version <= IdoResultParser.LatestVersion, "the IDO writes a newer format than the app can read");
    }

    [Fact]
    public void A_null_reader_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => ResultSetJson.Serialize(null!));
    }
}
