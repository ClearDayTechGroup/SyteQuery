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

        var normalizedSql = NormalizeSql(sql);

        // Check for missing WHERE clause
        CheckMissingWhereClause(normalizedSql, result);

        // Check for SELECT *
        CheckSelectStar(normalizedSql, result);

        // Check for missing TOP/LIMIT - pass original SQL for modification
        CheckMissingRowLimit(normalizedSql, sql, result);

        // Check for potentially expensive operations
        CheckExpensiveOperations(normalizedSql, result);

        return result;
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
        var hasSelectStar = Regex.IsMatch(sql, @"\bSELECT\s+\*\b", RegexOptions.IgnoreCase);

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
