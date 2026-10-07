namespace SyteQuery.Features.IntelliSense.Models;

/// <summary>
/// IntelliSense completion data for the SQL editor
/// </summary>
public sealed record IntelliSenseData(
    List<CompletionItem> Keywords,
    List<CompletionItem> Tables,
    List<CompletionItem> Views,
    List<CompletionItem> StoredProcedures,
    List<CompletionItem> Functions,
    List<SnippetCompletionItem> Snippets);

/// <summary>
/// A single completion item
/// </summary>
public sealed record CompletionItem(
    string Label,
    string Kind, // "Keyword", "Table", "View", "StoredProcedure", "Function"
    string? Detail,
    string? Documentation);

/// <summary>
/// A column completion item with additional metadata
/// </summary>
public sealed record ColumnCompletionItem(
    string Name,
    string DataType,
    bool IsNullable,
    bool IsPrimaryKey,
    string? Detail);

/// <summary>
/// A snippet completion item with expansion template
/// </summary>
public sealed record SnippetCompletionItem(
    string Prefix,
    string Label,
    string Description,
    string Template);
