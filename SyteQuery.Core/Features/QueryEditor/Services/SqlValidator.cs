using System.Text;
using System.Text.RegularExpressions;

namespace SyteQuery.Features.QueryEditor.Services;

/// <summary>
/// Validates SQL queries for prohibited operations.
/// </summary>
public static class SqlValidator
{
    // Matches CREATE/ALTER/DROP for procedures, views, functions, triggers and captures the
    // object type plus the remainder of the statement (tail) so we can extract a proc name.
    private static readonly Regex DdlRegex = new(
        @"\b(CREATE|ALTER|DROP)\s+(?:OR\s+ALTER\s+)?(?<type>PROC|PROCEDURE|VIEW|FUNCTION|TRIGGER)\b(?<tail>[^;]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Fallback guard: if DDL tokens appear in order but the strict matcher fails, still block.
    // Uses [\s\S]* to span across newlines and other characters.
    private static readonly Regex DdlTokenRegex = new(
        @"\b(CREATE|ALTER|DROP)\b[\s\S]*\b(PROC|PROCEDURE|VIEW|FUNCTION|TRIGGER)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Extracts a procedure name from the tail of a DDL statement.
    // Supports optional IF EXISTS, bracketed identifiers, quoted identifiers, and schema prefixes.
    private static readonly Regex ProcNameRegex = new(
        @"^\s*(?:IF\s+EXISTS\s+)?(?<name>(?:\[[^\]]+\]|""[^""]+""|[A-Za-z0-9_]+)(?:\s*\.\s*(?:\[[^\]]+\]|""[^""]+""|[A-Za-z0-9_]+))*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Checks if SQL contains prohibited DDL statements.
    /// Scans both direct SQL and string literals for dynamic SQL.
    /// </summary>
    public static bool ContainsProhibitedDdl(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        // Normalize and remove comments
        var normalized = RemoveLeadingComments(sql);

        // Check direct SQL
        if (ContainsProhibitedDdlInText(normalized))
            return true;

        // Check string literals (for dynamic SQL)
        return ContainsProhibitedDdlInStringLiterals(sql);
    }

    private static bool ContainsProhibitedDdlInText(string text)
    {
        var anyMatch = false;

        foreach (Match match in DdlRegex.Matches(text))
        {
            anyMatch = true;
            var type = match.Groups["type"].Value;

            if (type.Equals("VIEW", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("FUNCTION", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("TRIGGER", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (type.Equals("PROC", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("PROCEDURE", StringComparison.OrdinalIgnoreCase))
            {
                var name = ExtractProcedureName(match.Groups["tail"].Value);
                if (!IsExtgenProcedureName(name))
                    return true;
            }
        }

        if (DdlTokenRegex.IsMatch(text) && !anyMatch)
            return true;

        return false;
    }

    private static bool ContainsProhibitedDdlInStringLiterals(string sql)
    {
        for (int i = 0; i < sql.Length; i++)
        {
            if (sql[i] != '\'')
                continue;

            var literal = ExtractStringLiteral(sql, ref i);
            if (ContainsProhibitedDdlInText(literal))
                return true;
        }

        return false;
    }

    private static string? ExtractProcedureName(string tail)
    {
        var match = ProcNameRegex.Match(tail);
        if (!match.Success)
            return null;

        var fullName = match.Groups["name"].Value;
        var parts = fullName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var last = parts.Length > 0 ? parts[^1].Trim() : string.Empty;
        return UnwrapIdentifier(last);
    }

    private static string UnwrapIdentifier(string identifier)
    {
        if (identifier.Length >= 2 &&
            identifier[0] == '[' && identifier[^1] == ']')
        {
            return identifier[1..^1];
        }

        if (identifier.Length >= 2 &&
            identifier[0] == '"' && identifier[^1] == '"')
        {
            return identifier[1..^1];
        }

        return identifier;
    }

    private static bool IsExtgenProcedureName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.StartsWith("extgen", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveLeadingComments(string sql)
    {
        var span = sql.AsSpan().TrimStart();

        // Remove BOM if present
        if (span.Length > 0 && span[0] == '\uFEFF')
            span = span[1..].TrimStart();

        // Remove leading comments
        while (true)
        {
            if (span.StartsWith("--"))
            {
                var newline = span.IndexOf('\n');
                span = newline >= 0 ? span[(newline + 1)..].TrimStart() : ReadOnlySpan<char>.Empty;
                continue;
            }

            if (span.StartsWith("/*"))
            {
                var end = span.IndexOf("*/");
                span = end >= 0 ? span[(end + 2)..].TrimStart() : ReadOnlySpan<char>.Empty;
                continue;
            }

            break;
        }

        return span.ToString();
    }

    private static string ExtractStringLiteral(string sql, ref int index)
    {
        index++; // Skip opening quote
        var sb = new StringBuilder();

        while (index < sql.Length)
        {
            if (sql[index] == '\'')
            {
                // Check for escaped quote
                if (index + 1 < sql.Length && sql[index + 1] == '\'')
                {
                    sb.Append('\'');
                    index += 2;
                    continue;
                }

                // End of string
                break;
            }

            sb.Append(sql[index]);
            index++;
        }

        return sb.ToString();
    }
}
