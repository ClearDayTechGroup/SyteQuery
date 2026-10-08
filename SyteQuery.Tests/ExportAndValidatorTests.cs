using ClosedXML.Excel;
using SyteQuery.Features.DataExport.Services;
using SyteQuery.Features.QueryEditor.Services;
using Xunit;

namespace SyteQuery.Tests;

public class ExcelExportServiceTests
{
    private static readonly ExcelExportService Service = new();

    private static List<Dictionary<string, object?>> Rows(params (string col, object? val)[][] rows) =>
        rows.Select(r => r.ToDictionary(c => c.col, c => c.val)).ToList();

    private static XLWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes));

    [Fact]
    public void One_result_set_is_one_sheet_with_headers_and_rows()
    {
        var bytes = Service.ExportToExcel(Rows(new[] { ("item", (object?)"A100"), ("qty", 4L) }), "Query Results");

        using var workbook = Open(bytes);
        var sheet = Assert.Single(workbook.Worksheets);
        Assert.Equal("Query Results", sheet.Name);
        Assert.Equal("item", sheet.Cell(1, 1).GetString());
        Assert.Equal("A100", sheet.Cell(2, 1).GetString());
        Assert.Equal(4, sheet.Cell(2, 2).GetDouble());
    }

    [Fact]
    public void No_rows_writes_a_note()
    {
        using var workbook = Open(Service.ExportToExcel(new List<Dictionary<string, object?>>()));
        Assert.Equal("No data to export", workbook.Worksheet(1).Cell(1, 1).GetString());
    }

    [Fact]
    public void Several_result_sets_are_one_sheet_each()
    {
        var sheets = new[]
        {
            new ExcelSheet("Result 1", new[] { "item" }, Rows(new[] { ("item", (object?)"A") })),
            new ExcelSheet("Result 2", new[] { "cust", "name" }, Rows(new[] { ("cust", (object?)1L), ("name", "x") })),
            new ExcelSheet("Result 3", new[] { "order_id", "status" }, new List<Dictionary<string, object?>>())
        };

        using var workbook = Open(Service.ExportToExcel(sheets));

        Assert.Equal(new[] { "Result 1", "Result 2", "Result 3" }, workbook.Worksheets.Select(s => s.Name));
        Assert.Equal("cust", workbook.Worksheet(2).Cell(1, 1).GetString());
    }

    [Fact]
    public void An_empty_result_set_still_has_its_header_row()
    {
        var sheets = new[] { new ExcelSheet("Result 1", new[] { "order_id", "status" }, new List<Dictionary<string, object?>>()) };

        using var workbook = Open(Service.ExportToExcel(sheets));

        Assert.Equal("order_id", workbook.Worksheet(1).Cell(1, 1).GetString());
        Assert.Equal("status", workbook.Worksheet(1).Cell(1, 2).GetString());
    }

    [Fact]
    public void Sheet_names_are_made_legal_and_unique()
    {
        var empty = new List<Dictionary<string, object?>>();
        var sheets = new[]
        {
            new ExcelSheet("Bad:name/[1]?", new[] { "a" }, empty),
            new ExcelSheet("Dup", new[] { "a" }, empty),
            new ExcelSheet("Dup", new[] { "a" }, empty),
            new ExcelSheet(new string('x', 50), new[] { "a" }, empty),
            new ExcelSheet("", new[] { "a" }, empty)
        };

        using var workbook = Open(Service.ExportToExcel(sheets));
        var names = workbook.Worksheets.Select(s => s.Name).ToList();

        Assert.Equal(5, names.Count);
        Assert.Equal(5, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(names, n => { Assert.InRange(n.Length, 1, 31); Assert.DoesNotContain(n, c => "[]:*?/\\".Contains(c)); });
    }

    [Fact]
    public void No_sheets_at_all_still_gives_a_valid_workbook()
    {
        using var workbook = Open(Service.ExportToExcel(Array.Empty<ExcelSheet>()));
        Assert.Equal("No data to export", workbook.Worksheet(1).Cell(1, 1).GetString());
    }
}

/// <summary>
/// The client-side DDL block. It is deliberately conservative (a regex that errs towards blocking) and is not a
/// security boundary - the IDO runs whatever it is sent - so these pin down what it blocks and what it lets through.
/// </summary>
public class SqlValidatorTests
{
    [Theory]
    [InlineData("create procedure dbo.x as select 1")]
    [InlineData("ALTER PROC dbo.x AS SELECT 1")]
    [InlineData("drop procedure dbo.x")]
    [InlineData("create view v as select 1 a")]
    [InlineData("alter function f() returns int as begin return 1 end")]
    [InlineData("create trigger tr on t after insert as select 1")]
    [InlineData("create or alter procedure dbo.x as select 1")]
    [InlineData("select 1; exec('create procedure dbo.x as select 1')")]   // inside a string literal
    public void Procedure_view_function_and_trigger_changes_are_blocked(string sql)
    {
        Assert.True(SqlValidator.ContainsProhibitedDdl(sql));
    }

    [Theory]
    [InlineData("create procedure dbo.extgen_MyThing as select 1")]
    [InlineData("alter procedure [dbo].[extgen_Other] as select 1")]
    public void Extgen_procedures_are_allowed(string sql)
    {
        Assert.False(SqlValidator.ContainsProhibitedDdl(sql));
    }

    [Theory]
    [InlineData("select * from item_mst")]
    [InlineData("update t set a = 1 where id = 1")]
    [InlineData("create table #t (a int)")]
    [InlineData("")]
    [InlineData("   ")]
    public void Ordinary_statements_are_not_blocked(string sql)
    {
        Assert.False(SqlValidator.ContainsProhibitedDdl(sql));
    }
}
