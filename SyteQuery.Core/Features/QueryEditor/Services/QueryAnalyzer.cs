using System.Text.RegularExpressions;
using SyteQuery.Features.QueryEditor.Models;

namespace SyteQuery.Features.QueryEditor.Services;

public sealed class QueryAnalyzer : IQueryAnalyzer
{
    private const int DefaultRowLimit = 1000;

    public QueryAnalysisResult Analyze(string sql)
    {
        var result = new QueryAnalysisResult();

        if (string.IsNullOrWhiteSpace(sql))
            return result;

        // The script is analysed one statement at a time. Looking at the whole text as one query meant a TOP in
        // one SELECT counted for every other SELECT in the script.
        var batches = SqlScriptParser.Parse(sql);
        if (batches.Count == 0)
            return result; // nothing but GO lines and whitespace

        var totalStatements = batches.Sum(b => b.ParsedOk ? Math.Max(b.Statements.Count, 1) : 1);
        var label = totalStatements > 1;
        var number = 0;
        var toRun = new List<string>();
        var limitApplied = false;

        foreach (var batch in batches)
        {
            if (!batch.ParsedOk)
            {
                // ScriptDom couldn't parse it (a typo, or syntax it doesn't know): fall back to the text checks on
                // the whole batch, so the warnings still appear and the server reports the real syntax error.
                number++;
                var legacy = new QueryAnalysisResult();
                AnalyzeWholeText(batch.Text, legacy);
                AddWarnings(result, legacy, label ? $"Statement {number}: " : "");
                toRun.Add(legacy.ShouldEnforceLimit && legacy.ModifiedQuery is not null ? legacy.ModifiedQuery : batch.Text);
                limitApplied |= legacy.ShouldEnforceLimit;
                continue;
            }

            var inserts = new List<int>();
            var possibleResultSets = 0;

            foreach (var statement in batch.Statements)
            {
                number++;
                var prefix = label ? $"Statement {number}: " : "";

                if (statement.Results != StatementResults.None)
                    possibleResultSets++;

                if (statement.IsQuery)
                {
                    var normalized = NormalizeSql(statement.Text);
                    var checks = new QueryAnalysisResult();
                    CheckMissingWhereClause(normalized, checks);
                    CheckSelectStar(normalized, checks);
                    CheckExpensiveOperations(normalized, checks);
                    AddWarnings(result, checks, prefix);

                    if (statement.LimitInsertOffset is { } offset)
                    {
                        inserts.Add(offset);
                        result.Warnings.Add(new QueryWarning
                        {
                            Severity = QueryWarningSeverity.Warning,
                            Message = prefix + "Query has no row limit",
                            Details = $"A TOP {DefaultRowLimit} limit will be automatically applied to prevent excessive data retrieval."
                        });
                    }
                    else if (statement.SetOperationHasNoTop)
                    {
                        result.Warnings.Add(new QueryWarning
                        {
                            Severity = QueryWarningSeverity.Info,
                            Message = prefix + "Row limit not applied",
                            Details = "A UNION / EXCEPT / INTERSECT query can't be limited automatically. Add TOP to its SELECTs if it may return many rows."
                        });
                    }
                }
                else if (statement.DmlWithoutWhere is { } dml)
                {
                    result.Warnings.Add(new QueryWarning
                    {
                        Severity = QueryWarningSeverity.Warning,
                        Message = prefix + $"{dml} has no WHERE clause",
                        Details = $"This {dml} will affect every row of the table."
                    });
                }
            }

            // Apply from the back so earlier offsets stay valid.
            var text = batch.Text;
            foreach (var offset in inserts.OrderByDescending(o => o))
                text = text.Insert(offset, $" TOP {DefaultRowLimit}");

            limitApplied |= inserts.Count > 0;
            toRun.Add(text);
            result.ExtraResultSets += Math.Max(0, possibleResultSets - 1);
        }

        result.Batches = toRun;
        result.ShouldEnforceLimit = limitApplied;
        result.ModifiedQuery = limitApplied ? string.Join(Environment.NewLine + "GO" + Environment.NewLine, toRun) : null;
        return result;
    }

    private static void AnalyzeWholeText(string sql, QueryAnalysisResult result)
    {
        var normalizedSql = NormalizeSql(sql);
        CheckMissingWhereClause(normalizedSql, result);
        CheckSelectStar(normalizedSql, result);
        CheckMissingRowLimit(normalizedSql, sql, result);
        CheckExpensiveOperations(normalizedSql, result);
    }

    private static void AddWarnings(QueryAnalysisResult into, QueryAnalysisResult from, string prefix)
    {
        foreach (var warning in from.Warnings)
        {
            into.Warnings.Add(new QueryWarning
            {
                Severity = warning.Severity,
                Message = prefix + warning.Message,
                Details = warning.Details
            });
        }
    }

    private static string NormalizeSql(string sql)
    {
        // Remove comments
        sql = Regex.Replace(sql, @"--.*$", "", RegexOptions.Multiline);
        sql = Regex.Replace(sql, @"/\*.*?\*/", "", RegexOptions.Singleline);

        // Normalize whitespace
        sql = Regex.Replace(sql, @"\s+", " ");

        return sql.Trim();
    }

    private static void CheckMissingWhereClause(string sql, QueryAnalysisResult result)
    {
        // Check if this is a SELECT statement
        if (!Regex.IsMatch(sql, @"\bSELECT\b", RegexOptions.IgnoreCase))
            return;

        // Skip if it's a simple SELECT without FROM (e.g., SELECT GETDATE())
        if (!Regex.IsMatch(sql, @"\bFROM\b", RegexOptions.IgnoreCase))
            return;

        // Check for WHERE clause
        var hasWhere = Regex.IsMatch(sql, @"\bWHERE\b", RegexOptions.IgnoreCase);

        if (!hasWhere)
        {
            result.Warnings.Add(new QueryWarning
            {
                Severity = QueryWarningSeverity.Warning,
                Message = "Query has no WHERE clause",
                Details = "This query will return all rows from the table(s), which may be slow and return large amounts of data."
            });
        }
    }

    private static void CheckSelectStar(string sql, QueryAnalysisResult result)
    {
        // Check for SELECT *
        // (An earlier pattern ended in \*\b, which can never match "SELECT * FROM": there is no word boundary
        // between '*' and a space. Also allow DISTINCT / ALL / TOP n before the star.)
        var hasSelectStar = Regex.IsMatch(
            sql,
            @"\bSELECT\s+(?:(?:DISTINCT|ALL)\s+)?(?:TOP\s*\(?\s*\d+\s*\)?\s+)?\*",
            RegexOptions.IgnoreCase);

        if (hasSelectStar)
        {
            result.Warnings.Add(new QueryWarning
            {
                Severity = QueryWarningSeverity.Info,
                Message = "Using SELECT *",
                Details = "Consider specifying only the columns you need instead of using SELECT * for better performance and clarity."
            });
        }
    }

    private static void CheckMissingRowLimit(string normalizedSql, string originalSql, QueryAnalysisResult result)
    {
        // Check if this is a SELECT statement
        if (!Regex.IsMatch(normalizedSql, @"\bSELECT\b", RegexOptions.IgnoreCase))
            return;

        // Check for TOP (with optional parentheses), FETCH, or LIMIT
        var hasLimit = Regex.IsMatch(normalizedSql, @"\bTOP\s*\(?\s*\d+\s*\)?\b", RegexOptions.IgnoreCase) ||
                      Regex.IsMatch(normalizedSql, @"\bFETCH\s+(FIRST|NEXT)\s+\d+\b", RegexOptions.IgnoreCase) ||
                      Regex.IsMatch(normalizedSql, @"\bLIMIT\s+\d+\b", RegexOptions.IgnoreCase);

        if (!hasLimit)
        {
            result.ShouldEnforceLimit = true;
            result.Warnings.Add(new QueryWarning
            {
                Severity = QueryWarningSeverity.Warning,
                Message = "Query has no row limit",
                Details = $"A TOP {DefaultRowLimit} limit will be automatically applied to prevent excessive data retrieval."
            });

            // Generate modified query with TOP clause - use original SQL to preserve formatting
            result.ModifiedQuery = ApplyTopLimit(originalSql, DefaultRowLimit);
        }
    }

    private static void CheckExpensiveOperations(string sql, QueryAnalysisResult result)
    {
        // Check for potential Cartesian product (multiple tables in FROM without JOIN)
        var fromMatch = Regex.Match(sql, @"\bFROM\s+([\w\.,\s]+?)(?:\bWHERE\b|\bGROUP\b|\bORDER\b|\bHAVING\b|$)", RegexOptions.IgnoreCase);
        if (fromMatch.Success)
        {
            var fromClause = fromMatch.Groups[1].Value;
            var tableCount = fromClause.Split(',').Length;
            var hasJoin = Regex.IsMatch(sql, @"\bJOIN\b", RegexOptions.IgnoreCase);

            if (tableCount > 1 && !hasJoin)
            {
                result.Warnings.Add(new QueryWarning
                {
                    Severity = QueryWarningSeverity.Warning,
                    Message = "Potential Cartesian product detected",
                    Details = "Multiple tables in FROM clause without explicit JOIN may result in a Cartesian product, causing poor performance."
                });
            }
        }

        // Check for DISTINCT with many columns (can be expensive)
        if (Regex.IsMatch(sql, @"\bDISTINCT\b", RegexOptions.IgnoreCase))
        {
            result.Warnings.Add(new QueryWarning
            {
                Severity = QueryWarningSeverity.Info,
                Message = "Using DISTINCT",
                Details = "DISTINCT operations can be expensive on large datasets. Ensure it's necessary."
            });
        }

        // Check for subqueries in SELECT clause (can be slow)
        if (Regex.IsMatch(sql, @"\bSELECT\b[^)]*\(\s*SELECT\b", RegexOptions.IgnoreCase))
        {
            result.Warnings.Add(new QueryWarning
            {
                Severity = QueryWarningSeverity.Info,
                Message = "Subquery in SELECT clause",
                Details = "Scalar subqueries in SELECT can execute once per row. Consider using a JOIN instead."
            });
        }
    }

    private static string ApplyTopLimit(string sql, int limit)
    {
        // Apply TOP N after the first SELECT keyword (main query only, not subqueries)
        // Only apply if there's no existing TOP, DISTINCT, or ALL keyword immediately after SELECT
        var pattern = @"(\bSELECT\b)\s+(?!TOP\b|DISTINCT\b|ALL\b)";

        // Use a MatchEvaluator to replace only the first occurrence
        var replaced = false;
        return Regex.Replace(sql, pattern, match =>
        {
            if (!replaced)
            {
                replaced = true;
                // Return the matched SELECT keyword + TOP N + space
                return $"{match.Groups[1].Value} TOP {limit} ";
            }
            return match.Value;
        }, RegexOptions.IgnoreCase);
    }
}
