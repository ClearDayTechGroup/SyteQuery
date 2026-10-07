namespace SyteQuery.Features.QueryEditor.Models;

public sealed class QueryAnalysisResult
{
    public bool HasWarnings => Warnings.Count > 0;
    public List<QueryWarning> Warnings { get; set; } = new();
    public bool ShouldEnforceLimit { get; set; }
    public string? ModifiedQuery { get; set; }
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
