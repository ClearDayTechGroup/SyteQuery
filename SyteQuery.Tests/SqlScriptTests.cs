using Microsoft.SqlServer.TransactSql.ScriptDom;
using SyteQuery.Features.QueryEditor.Models;
using SyteQuery.Features.QueryEditor.Services;
using Xunit;

namespace SyteQuery.Tests;

public class SqlScriptParserTests
{
    // ---- GO ----

    [Fact]
    public void Splits_on_a_GO_line()
    {
        Assert.Equal(2, SqlScriptParser.SplitOnGo("select 1\nGO\nselect 2").Count);
    }

    [Theory]
    [InlineData("select 1\ngo 5\nselect 2\nGo -- next\nselect 3", 3)]   // any case, repeat count, trailing comment
    [InlineData("/*\nGO\n*/ select 1", 1)]                              // inside a block comment
    [InlineData("select 'a\nGO\nb'", 1)]                                // inside a string
    [InlineData("select [a\nGO\nb] from t", 1)]                         // inside [identifier]
    [InlineData("select \"a\nGO\nb\" from t", 1)]                       // inside "identifier"
    [InlineData("select 1\nGOTO x\nselect 2", 1)]                       // GO as part of a word
    [InlineData("select 1 -- GO\nselect 2", 1)]                         // GO in a line comment
    public void GO_is_recognised_only_where_it_is_a_command(string sql, int expectedBatches)
    {
        Assert.Equal(expectedBatches, SqlScriptParser.SplitOnGo(sql).Count);
    }

    [Fact]
    public void Nested_block_comments_are_followed()
    {
        Assert.Single(SqlScriptParser.SplitOnGo("/* outer /* inner */\nGO\n still comment */ select 1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    [InlineData("GO\n\n  GO  ")]
    public void Nothing_but_GO_lines_and_whitespace_has_no_batches(string sql)
    {
        Assert.Empty(SqlScriptParser.SplitOnGo(sql));
        Assert.Empty(SqlScriptParser.Parse(sql));
    }

    [Fact]
    public void The_GO_line_itself_is_not_part_of_any_batch()
    {
        Assert.All(SqlScriptParser.SplitOnGo("select 1\nGO\nselect 2"), b => Assert.DoesNotContain("GO", b));
    }

    // ---- statements ----

    private static SqlStatementInfo Only(string sql)
    {
        var batch = Assert.Single(SqlScriptParser.Parse(sql));
        Assert.True(batch.ParsedOk);
        return Assert.Single(batch.Statements);
    }

    [Fact]
    public void Two_selects_without_semicolons_are_two_statements()
    {
        var batch = Assert.Single(SqlScriptParser.Parse("select top 1 * from item_mst\nselect top 1 * from customer_mst"));

        Assert.Equal(2, batch.Statements.Count);
        Assert.All(batch.Statements, s => Assert.Equal(StatementResults.Yes, s.Results));
    }

    [Theory]
    [InlineData("select * from t", StatementResults.Yes)]
    [InlineData("select 1", StatementResults.Yes)]
    [InlineData("select a from t union select a from u", StatementResults.Yes)]
    [InlineData("with c as (select 1 x) select * from c", StatementResults.Yes)]
    [InlineData("select * into #t from u", StatementResults.None)]
    [InlineData("declare @x int = 1", StatementResults.None)]
    [InlineData("insert t values (1)", StatementResults.None)]
    [InlineData("update t set a = 1 where id = 1", StatementResults.None)]
    [InlineData("exec sp_who", StatementResults.Maybe)]
    [InlineData("if 1 = 1 select 1", StatementResults.Maybe)]
    [InlineData("begin select 1 end", StatementResults.Maybe)]
    public void Statements_are_classified_by_whether_they_return_a_result_set(string sql, StatementResults expected)
    {
        Assert.Equal(expected, Only(sql).Results);
    }

    [Fact]
    public void A_variable_assignment_select_returns_nothing()
    {
        var batch = Assert.Single(SqlScriptParser.Parse("declare @x int; select @x = count(*) from t"));

        Assert.Equal(2, batch.Statements.Count);
        Assert.Equal(StatementResults.None, batch.Statements[1].Results);
    }

    [Fact]
    public void Text_that_does_not_parse_is_flagged_not_thrown()
    {
        var batch = Assert.Single(SqlScriptParser.Parse("select * from where"));

        Assert.False(batch.ParsedOk);
        Assert.Empty(batch.Statements);
        Assert.Contains("from where", batch.Text);
    }

    [Fact]
    public void An_update_or_delete_without_where_is_noticed()
    {
        var batch = Assert.Single(SqlScriptParser.Parse("update t set x = 1; delete from u; update t set x = 2 where id = 1; delete from u where id = 1"));

        Assert.Equal("UPDATE", batch.Statements[0].DmlWithoutWhere);
        Assert.Equal("DELETE", batch.Statements[1].DmlWithoutWhere);
        Assert.Null(batch.Statements[2].DmlWithoutWhere);
        Assert.Null(batch.Statements[3].DmlWithoutWhere);
    }
}

public class QueryAnalyzerTests
{
    private static readonly QueryAnalyzer Analyzer = new();

    private static bool Parses(string sql)
    {
        var parser = new TSql170Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        parser.Parse(reader, out var errors);
        return errors.Count == 0;
    }

    private static string Messages(QueryAnalysisResult r) => string.Join(" | ", r.Warnings.Select(w => w.Message));

    // ---- the row limit ----

    [Fact]
    public void A_plain_select_gets_a_limit_after_SELECT()
    {
        var r = Analyzer.Analyze("select * from item_mst");

        Assert.True(r.ShouldEnforceLimit);
        Assert.Equal("select TOP 1000 * from item_mst", Assert.Single(r.Batches));
        Assert.Equal(r.Batches[0], r.ModifiedQuery);
    }

    [Fact]
    public void The_issue_3_example_needs_no_limit_and_expects_two_result_sets()
    {
        var r = Analyzer.Analyze("select top 1 * from item_mst\nselect top 1 * from customer_mst");

        Assert.False(r.ShouldEnforceLimit);
        Assert.Single(r.Batches);
        Assert.Equal(1, r.ExtraResultSets);
        Assert.DoesNotContain(r.Warnings, w => w.Message.Contains("no row limit"));
    }

    [Fact]
    public void A_TOP_in_one_select_does_not_count_for_the_others()
    {
        var r = Analyzer.Analyze("select * from a; select top 1 * from b;");

        Assert.Equal("select TOP 1000 * from a; select top 1 * from b;", r.Batches[0]);
        Assert.Contains(r.Warnings, w => w.Message == "Statement 1: Query has no row limit");
        Assert.DoesNotContain(r.Warnings, w => w.Message.StartsWith("Statement 2: Query has no row limit"));
    }

    [Theory]
    [InlineData("select distinct name from t", "select distinct TOP 1000 name from t")]
    [InlineData("select all name from t", "select all TOP 1000 name from t")]
    [InlineData("select /*why*/ distinct name from t", "select /*why*/ distinct TOP 1000 name from t")]
    [InlineData("with c as (select 1 x from t) select * from c", "with c as (select 1 x from t) select TOP 1000 * from c")]
    [InlineData("select * from (select top 5 * from t) x", "select TOP 1000 * from (select top 5 * from t) x")]
    public void The_limit_goes_where_T_SQL_wants_it(string sql, string expected)
    {
        var rewritten = Analyzer.Analyze(sql).Batches[0];

        Assert.Equal(expected, rewritten);
        Assert.True(Parses(rewritten), rewritten);
    }

    [Theory]
    [InlineData("select 1 as ok")]                                                          // no FROM
    [InlineData("select top (5) * from t")]
    [InlineData("select top 5 percent * from t")]
    [InlineData("select * from t order by a offset 0 rows fetch next 10 rows only")]
    [InlineData("select a from t union select a from u")]                                   // set operations aren't rewritten
    [InlineData("select * into #t from u")]
    [InlineData("insert t values (1)")]
    public void These_are_left_alone(string sql)
    {
        var r = Analyzer.Analyze(sql);

        Assert.False(r.ShouldEnforceLimit);
        Assert.Equal(sql, Assert.Single(r.Batches));
        Assert.Null(r.ModifiedQuery);
    }

    [Theory]
    [InlineData("select a,b from t")]
    [InlineData("select\n  a\nfrom t\nwhere a=1")]
    [InlineData("SELECT * FROM a JOIN b ON a.x=b.x")]
    [InlineData("select * from t order by a")]
    [InlineData("select a from t for xml path('')")]
    [InlineData("select * from t option (maxdop 1)")]
    public void Every_rewrite_is_still_valid_T_SQL(string sql)
    {
        Assert.True(Parses(Analyzer.Analyze(sql).Batches[0]));
    }

    [Fact]
    public void A_union_gets_an_info_note_instead_of_a_limit()
    {
        var r = Analyzer.Analyze("select a from t union select a from u");

        Assert.Contains(r.Warnings, w => w.Message.Contains("Row limit not applied") && w.Severity == QueryWarningSeverity.Info);
        Assert.Equal(0, r.ExtraResultSets);
    }

    // ---- GO ----

    [Fact]
    public void Each_batch_is_limited_on_its_own_and_GO_is_not_sent()
    {
        var r = Analyzer.Analyze("select * from a\nGO\nselect * from b");

        Assert.Equal(2, r.Batches.Count);
        Assert.All(r.Batches, b => { Assert.Contains("TOP 1000", b); Assert.DoesNotContain("GO", b); });
    }

    [Fact]
    public void Nothing_to_run_gives_no_batches()
    {
        Assert.Empty(Analyzer.Analyze("GO\n\n  GO  ").Batches);
        Assert.Empty(Analyzer.Analyze("").Batches);
    }

    // ---- result-set expectations (used to tell the user an old IDO is hiding some) ----

    [Theory]
    [InlineData("select 1", 0)]
    [InlineData("select 1; select 2", 1)]
    [InlineData("select 1; select 2; select 3", 2)]
    [InlineData("declare @x int; select @x = count(*) from t; select * from t where 1=1; select 2", 1)]
    [InlineData("select * into #t from u; select * from #t", 0)]
    [InlineData("exec sp_who; select 1", 1)]
    [InlineData("select 1\nGO\nselect 2", 0)]   // an old IDO still returns each batch's first set
    public void Extra_result_sets_are_counted_per_batch(string sql, int expected)
    {
        Assert.Equal(expected, Analyzer.Analyze(sql).ExtraResultSets);
    }

    // ---- warnings ----

    [Fact]
    public void A_single_query_has_unlabelled_messages()
    {
        var r = Analyzer.Analyze("select * from item_mst");

        Assert.All(r.Warnings, w => Assert.DoesNotContain("Statement", w.Message));
        Assert.Contains(r.Warnings, w => w.Message == "Query has no WHERE clause");
        Assert.Contains(r.Warnings, w => w.Message == "Using SELECT *");
        Assert.Contains(r.Warnings, w => w.Message == "Query has no row limit");
    }

    [Fact]
    public void Several_statements_label_their_messages()
    {
        var r = Analyzer.Analyze("select * from a; select * from b where id = 1");

        Assert.Contains(r.Warnings, w => w.Message == "Statement 1: Query has no WHERE clause");
        Assert.DoesNotContain(r.Warnings, w => w.Message == "Statement 2: Query has no WHERE clause");
    }

    [Theory]
    [InlineData("select * from t", true)]
    [InlineData("select distinct * from t", true)]
    [InlineData("select top 5 * from t", true)]
    [InlineData("select a from t", false)]
    [InlineData("select count(*) from t", false)]
    public void The_SELECT_star_tip_fires_when_it_should(string sql, bool expected)
    {
        Assert.Equal(expected, Analyzer.Analyze(sql).Warnings.Any(w => w.Message.Contains("Using SELECT *")));
    }

    [Fact]
    public void An_update_or_delete_without_where_is_warned_with_its_statement_number()
    {
        var r = Analyzer.Analyze("update t set x = 1; delete from u; update t set x = 2 where id = 1");

        Assert.Contains(r.Warnings, w => w.Message == "Statement 1: UPDATE has no WHERE clause");
        Assert.Contains(r.Warnings, w => w.Message == "Statement 2: DELETE has no WHERE clause");
        Assert.DoesNotContain(r.Warnings, w => w.Message.StartsWith("Statement 3"));
    }

    [Fact]
    public void Unparseable_text_falls_back_to_the_text_checks_and_is_passed_through()
    {
        var r = Analyzer.Analyze("select * from where");

        Assert.Contains("from where", Assert.Single(r.Batches));
        Assert.NotEmpty(r.Warnings);
    }
}
