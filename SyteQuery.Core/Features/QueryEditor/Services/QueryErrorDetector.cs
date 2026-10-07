namespace SyteQuery.Features.QueryEditor.Services;

/// <summary>
/// The Query Tool IDO reports a SQL error (bad object name, syntax error...) as a *successful*
/// call whose message parameter carries the SQL Server error text and whose data is empty - so
/// the transport says "ok" while the query actually failed. The web version caught this by
/// looking for words like "error"/"invalid"/"fail" in the message; this is the same idea, a bit
/// broader, and only applied when no rows came back (rows mean the query ran).
/// </summary>
public static class QueryErrorDetector
{
    private static readonly string[] ErrorPhrases =
    {
        "error", "invalid", "fail", "exception", "incorrect syntax", "syntax", "cannot ", "could not",
        "violation", "denied", "does not exist", "ambiguous", "must declare", "unclosed",
        "overflow", "divide by zero", "conversion", "unknown", "not allowed", "timeout", "deadlock",
        "arithmetic", "not found", "is not a valid", "too many", "truncated", "constraint"
    };

    public static bool LooksLikeError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        foreach (var phrase in ErrorPhrases)
        {
            if (message.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
