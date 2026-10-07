using SyteQuery.Features.QueryEditor.Models;

namespace SyteQuery.Features.QueryEditor.Services;

public interface IQueryAnalyzer
{
    QueryAnalysisResult Analyze(string sql);
}
