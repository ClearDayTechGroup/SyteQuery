namespace SyteQuery.Features.QueryEditor.Models;

public sealed class QueryAnalysisResult
{
    public bool HasWarnings => Warnings.Count > 0;
    public List<QueryWarning> Warnings { get; set; } = new();
    public bool ShouldEnforceLimit { get; set; }

    /// <summary>The whole script with the row limit applied (batches separated by GO lines). Null when nothing changed.</summary>
    public string? ModifiedQuery { get; set; }

    /// <summary>
    /// What to actually run: one entry per batch (the text between GO lines), with the row limit already
    /// applied to each unbounded SELECT. A script without GO has exactly one.
    /// </summary>
    public IReadOnlyList<string> Batches { get; set; } = Array.Empty<string>();

    /// <summary>
    /// How many result sets beyond one-per-batch the script looks likely to produce (SELECTs, plus EXEC and
    /// blocks that might). An IDO that only returns the first result set would hide this many.
    /// </summary>
    public int ExtraResultSets { get; set; }
}

public sealed class QueryWarning
{
    public QueryWarningSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public enum QueryWarningSeverity
{
    Info,
    Warning,
    Error
}
