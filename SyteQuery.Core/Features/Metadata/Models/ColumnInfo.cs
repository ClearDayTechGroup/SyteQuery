namespace SyteQuery.Features.Metadata.Models;

/// <summary>
/// Represents detailed information about a database column.
/// </summary>
public sealed record ColumnInfo(
    string Name,
    string DataType,
    string? MaxLength,
    bool IsNullable,
    bool IsPrimaryKey);
