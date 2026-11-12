using SyteQuery.Desktop.Results;
using Xunit;

namespace SyteQuery.Tests;

/// <summary>The text that "Copy" puts on the clipboard for result cells.</summary>
public class ResultsClipboardTests
{
    private static IReadOnlyList<IReadOnlyList<string?>> Rows(params string?[][] rows) => rows.Select(r => (IReadOnlyList<string?>)r).ToList();

    [Fact]
    public void One_cell_is_just_its_text_with_no_trailing_newline()
    {
        Assert.Equal("A100", ResultsClipboard.Build(new[] { "item" }, Rows(new[] { "A100" }), includeHeaders: false));
    }

    [Fact]
    public void Cells_are_tab_separated_and_rows_newline_separated()
    {
        var text = ResultsClipboard.Build(new[] { "item", "qty" }, Rows(new[] { "A100", "4" }, new[] { "B200", "9" }), includeHeaders: false);

        Assert.Equal("A100\t4\nB200\t9", text);
    }

    [Fact]
    public void Headers_go_first_when_asked_for()
    {
        var text = ResultsClipboard.Build(new[] { "item", "qty" }, Rows(new[] { "A100", "4" }), includeHeaders: true);

        Assert.Equal("item\tqty\nA100\t4", text);
    }

    [Fact]
    public void An_unselected_cell_in_the_block_is_empty_and_does_not_shift_the_others()
    {
        // a Ctrl-click selection with a gap: (row 1, col 1) is not selected
        var text = ResultsClipboard.Build(new[] { "a", "b" }, Rows(new[] { "1", "2" }, new string?[] { "3", null }), includeHeaders: false);

        Assert.Equal("1\t2\n3\t", text);
    }

    [Theory]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData("has\ttab", "\"has\ttab\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("carriage\rreturn", "\"carriage\rreturn\"")]
    public void A_value_that_would_break_the_layout_is_quoted_the_way_Excel_expects(string value, string expected)
    {
        Assert.Equal(expected, ResultsClipboard.Quote(value));
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("with, comma")]
    [InlineData("it's fine")]
    [InlineData("")]
    public void Ordinary_values_are_left_alone(string value)
    {
        Assert.Equal(value, ResultsClipboard.Quote(value));
    }

    [Fact]
    public void A_quoted_value_does_not_disturb_its_neighbours()
    {
        var text = ResultsClipboard.Build(new[] { "a", "b" }, Rows(new[] { "x\ny", "z" }), includeHeaders: false);

        Assert.Equal("\"x\ny\"\tz", text);
    }

    [Fact]
    public void Null_and_empty_values_come_out_empty()
    {
        Assert.Equal("\t\tend", ResultsClipboard.Build(new[] { "a", "b", "c" }, Rows(new string?[] { null, "", "end" }), includeHeaders: false));
    }

    [Fact]
    public void Nothing_selected_gives_empty_text()
    {
        Assert.Equal("", ResultsClipboard.Build(Array.Empty<string>(), Rows(), includeHeaders: false));
    }
}
