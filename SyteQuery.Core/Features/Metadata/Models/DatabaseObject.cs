namespace SyteQuery.Features.Metadata.Models;

/// <summary>
/// Represents a database object (table, view, stored procedure, function) with schema and name.
/// </summary>
public sealed record DatabaseObject(string SchemaName, string ObjectName);
