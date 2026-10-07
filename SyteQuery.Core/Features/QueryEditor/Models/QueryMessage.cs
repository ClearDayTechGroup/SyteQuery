namespace SyteQuery.Features.QueryEditor.Models;

public enum QueryMessageLevel { Info, Warning, Error }

public sealed record QueryMessage(DateTime TimeUtc, QueryMessageLevel Level, string Text);
