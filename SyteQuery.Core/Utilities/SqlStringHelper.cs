namespace SyteQuery.Utilities;

/// <summary>
/// Utilities for SQL string handling and escaping
/// </summary>
public static class SqlStringHelper
{
    /// <summary>
    /// Escapes single quotes in a SQL string literal to prevent SQL injection.
    /// Replaces each single quote (') with two single quotes ('').
    /// </summary>
    /// <param name="input">The string to escape</param>
    /// <returns>The escaped string safe for use in SQL string literals</returns>
    public static string EscapeSqlString(string input)
    {
        return input?.Replace("'", "''") ?? string.Empty;
    }

    /// <summary>
    /// Escapes both schema and object names for SQL usage
    /// </summary>
    /// <param name="schemaName">The schema name to escape</param>
    /// <param name="objectName">The object name to escape</param>
    /// <returns>Tuple of (escapedSchema, escapedObject)</returns>
    public static (string schema, string name) EscapeObjectName(string schemaName, string objectName)
    {
        return (EscapeSqlString(schemaName), EscapeSqlString(objectName));
    }
}
