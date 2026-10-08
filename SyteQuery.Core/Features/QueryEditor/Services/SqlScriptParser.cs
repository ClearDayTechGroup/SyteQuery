using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SyteQuery.Features.QueryEditor.Services;

/// <summary>Whether a statement produces a result set.</summary>
public enum StatementResults
{
    /// <summary>It doesn't (INSERT, UPDATE, DECLARE, SELECT INTO, SELECT @x = ...).</summary>
    None,

    /// <summary>It might (EXEC, or a BEGIN/IF/WHILE block that could contain a SELECT).</summary>
    Maybe,

    /// <summary>A SELECT that returns rows.</summary>
    Yes
}

/// <summary>One statement of a batch, as far as the app needs to know about it.</summary>
public sealed class SqlStatementInfo
{
    public string Text { get; init; } = string.Empty;
    public StatementResults Results { get; init; }

    /// <summary>A SELECT that returns rows - the checks for missing WHERE, SELECT *, and so on apply to it.</summary>
    public bool IsQuery { get; init; }

    /// <summary>UNION / EXCEPT / INTERSECT: a row limit can't simply be inserted.</summary>
    public bool IsSetOperation { get; init; }

    /// <summary>A set operation in which no branch has a TOP.</summary>
    public bool SetOperationHasNoTop { get; init; }

    /// <summary>
    /// Where (an offset into the batch text) " TOP n" would go to limit this query - right after SELECT
    /// [DISTINCT|ALL] - or null when no limit is needed or possible (it already has TOP/OFFSET, no FROM,
    /// SELECT INTO, a variable assignment, a set operation).
    /// </summary>
    public int? LimitInsertOffset { get; init; }

    /// <summary>"UPDATE" or "DELETE" when the statement has no WHERE clause, otherwise null.</summary>
    public string? DmlWithoutWhere { get; init; }
}

/// <summary>A batch: the text between GO lines.</summary>
/// <param name="Text">The batch text.</param>
/// <param name="ParsedOk">False when ScriptDom couldn't parse it; <paramref name="Statements"/> is then empty and callers fall back to text checks.</param>
public sealed record SqlBatchInfo(string Text, IReadOnlyList<SqlStatementInfo> Statements, bool ParsedOk);

/// <summary>
/// Splits a script into batches (on GO lines) and statements (with the T-SQL parser), so each statement
/// can be analysed on its own. The regex checks the app used before looked at the whole text, which broke
/// as soon as it held more than one statement: a TOP in one SELECT counted for all of them, and
/// statements run together without semicolons - as in a typical "two SELECTs" script - can't be told apart
/// by text matching.
/// </summary>
public static class SqlScriptParser
{
    private static readonly Regex GoLine = new(
        @"^\s*GO\s*(?:\d+)?\s*(?:--.*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<SqlBatchInfo> Parse(string sql)
    {
        var batches = new List<SqlBatchInfo>();
        foreach (var text in SplitOnGo(sql))
            batches.Add(ParseBatch(text));
        return batches;
    }

    // ------------------------------------------------------------
    // GO
    // ------------------------------------------------------------

    /// <summary>
    /// Splits on lines that are just GO (like SSMS does - GO is a client directive, not T-SQL), ignoring any
    /// that sit inside a comment, string or quoted identifier. An optional repeat count is accepted but ignored.
    /// Empty batches are dropped.
    /// </summary>
    public static IReadOnlyList<string> SplitOnGo(string sql)
    {
        var batches = new List<string>();
        var current = new StringBuilder();
        var state = new ScanState();

        foreach (var line in EnumerateLines(sql))
        {
            var content = line.TrimEnd('\r', '\n');
            if (state.IsPlain && GoLine.IsMatch(content))
            {
                Flush();
                continue;
            }

            current.Append(line);
            state.Advance(content);
        }

        Flush();
        return batches;

        void Flush()
        {
            if (!string.IsNullOrWhiteSpace(current.ToString()))
                batches.Add(current.ToString());
            current.Clear();
        }
    }

    private static IEnumerable<string> EnumerateLines(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var newline = text.IndexOf('\n', start);
            if (newline < 0)
            {
                yield return text[start..];
                yield break;
            }

            yield return text[start..(newline + 1)];
            start = newline + 1;
        }
    }

    /// <summary>Tracks whether the scan is inside a block comment, string or quoted identifier across lines.</summary>
    private sealed class ScanState
    {
        private int _blockComment;
        private bool _inString;
        private bool _inBracket;
        private bool _inQuoted;

        public bool IsPlain => _blockComment == 0 && !_inString && !_inBracket && !_inQuoted;

        public void Advance(string line)
        {
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                var next = i + 1 < line.Length ? line[i + 1] : '\0';

                if (_blockComment > 0)
                {
                    if (c == '/' && next == '*') { _blockComment++; i++; }
                    else if (c == '*' && next == '/') { _blockComment--; i++; }
                }
                else if (_inString)
                {
                    if (c == '\'' && next == '\'') i++;
                    else if (c == '\'') _inString = false;
                }
                else if (_inBracket)
                {
                    if (c == ']' && next == ']') i++;
                    else if (c == ']') _inBracket = false;
                }
                else if (_inQuoted)
                {
                    if (c == '"' && next == '"') i++;
                    else if (c == '"') _inQuoted = false;
                }
                else if (c == '-' && next == '-')
                {
                    return; // rest of the line is a comment
                }
                else if (c == '/' && next == '*') { _blockComment = 1; i++; }
                else if (c == '\'') _inString = true;
                else if (c == '[') _inBracket = true;
                else if (c == '"') _inQuoted = true;
            }
        }
    }

    // ------------------------------------------------------------
    // Statements
    // ------------------------------------------------------------

    private static SqlBatchInfo ParseBatch(string batchText)
    {
        var parser = new TSql170Parser(initialQuotedIdentifiers: true);
        TSqlFragment? fragment;
        IList<ParseError> errors;
        using (var reader = new StringReader(batchText))
            fragment = parser.Parse(reader, out errors);

        if (errors is { Count: > 0 } || fragment is not TSqlScript script)
            return new SqlBatchInfo(batchText, Array.Empty<SqlStatementInfo>(), ParsedOk: false);

        var statements = new List<SqlStatementInfo>();
        foreach (var batch in script.Batches)
        {
            foreach (var statement in batch.Statements)
                statements.Add(Describe(statement, batchText));
        }

        return new SqlBatchInfo(batchText, statements, ParsedOk: true);
    }

    private static SqlStatementInfo Describe(TSqlStatement statement, string batchText)
    {
        var text = batchText.Substring(statement.StartOffset, statement.FragmentLength);

        switch (statement)
        {
            case SelectStatement select:
                return DescribeSelect(select, text);

            case UpdateStatement update when update.UpdateSpecification.WhereClause is null:
                return new SqlStatementInfo { Text = text, Results = StatementResults.None, DmlWithoutWhere = "UPDATE" };

            case DeleteStatement delete when delete.DeleteSpecification.WhereClause is null:
                return new SqlStatementInfo { Text = text, Results = StatementResults.None, DmlWithoutWhere = "DELETE" };

            case ExecuteStatement:
            case BeginEndBlockStatement:
            case IfStatement:
            case WhileStatement:
            case TryCatchStatement:
                return new SqlStatementInfo { Text = text, Results = StatementResults.Maybe };

            default:
                return new SqlStatementInfo { Text = text, Results = StatementResults.None };
        }
    }

    private static SqlStatementInfo DescribeSelect(SelectStatement select, string text)
    {
        // SELECT ... INTO creates a table and returns nothing.
        if (select.Into is not null)
            return new SqlStatementInfo { Text = text, Results = StatementResults.None };

        var specs = new List<QuerySpecification>();
        CollectSpecifications(select.QueryExpression, specs);

        // SELECT @x = col ... assigns variables and returns nothing.
        if (specs.Any(s => s.SelectElements.OfType<SelectSetVariable>().Any()))
            return new SqlStatementInfo { Text = text, Results = StatementResults.None };

        if (select.QueryExpression is QuerySpecification spec)
        {
            var needsLimit = spec.TopRowFilter is null && spec.FromClause is not null && select.QueryExpression.OffsetClause is null;
            return new SqlStatementInfo
            {
                Text = text,
                Results = StatementResults.Yes,
                IsQuery = true,
                LimitInsertOffset = needsLimit ? FindLimitInsertOffset(spec) : null
            };
        }

        var isSetOperation = select.QueryExpression is BinaryQueryExpression;
        return new SqlStatementInfo
        {
            Text = text,
            Results = StatementResults.Yes,
            IsQuery = true,
            IsSetOperation = isSetOperation,
            SetOperationHasNoTop = isSetOperation && specs.All(s => s.TopRowFilter is null)
        };
    }

    private static void CollectSpecifications(QueryExpression? expression, List<QuerySpecification> into)
    {
        switch (expression)
        {
            case QuerySpecification spec:
                into.Add(spec);
                break;
            case BinaryQueryExpression binary:
                CollectSpecifications(binary.FirstQueryExpression, into);
                CollectSpecifications(binary.SecondQueryExpression, into);
                break;
            case QueryParenthesisExpression parenthesis:
                CollectSpecifications(parenthesis.QueryExpression, into);
                break;
        }
    }

    /// <summary>The offset just after SELECT (and DISTINCT / ALL when present), where " TOP n" goes.</summary>
    private static int? FindLimitInsertOffset(QuerySpecification spec)
    {
        var tokens = spec.ScriptTokenStream;
        if (tokens is null || spec.FirstTokenIndex < 0 || spec.FirstTokenIndex >= tokens.Count)
            return null;

        var select = tokens[spec.FirstTokenIndex];
        if (select.TokenType != TSqlTokenType.Select)
            return null;

        var insertAfter = select;
        if (spec.UniqueRowFilter != UniqueRowFilter.NotSpecified)
        {
            for (var i = spec.FirstTokenIndex + 1; i <= spec.LastTokenIndex && i < tokens.Count; i++)
            {
                if (tokens[i].TokenType is TSqlTokenType.WhiteSpace or TSqlTokenType.SingleLineComment or TSqlTokenType.MultilineComment)
                    continue;

                if (tokens[i].TokenType is TSqlTokenType.Distinct or TSqlTokenType.All)
                    insertAfter = tokens[i];
                break;
            }
        }

        return insertAfter.Offset + insertAfter.Text.Length;
    }
}
