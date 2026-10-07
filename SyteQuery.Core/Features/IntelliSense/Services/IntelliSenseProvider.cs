using System.Collections.Concurrent;
using SyteQuery.Features.IntelliSense.Models;
using SyteQuery.Features.Metadata.Services;

namespace SyteQuery.Features.IntelliSense.Services;

/// <summary>
/// Provides IntelliSense completion data for SQL editor with caching per environment
/// </summary>
public sealed class IntelliSenseProvider : IIntelliSenseProvider
{
    private readonly IMetadataCache _metadataCache;
    private readonly ConcurrentDictionary<string, IntelliSenseData> _completionCache = new();

    // SQL Keywords for T-SQL
    private static readonly List<CompletionItem> SqlKeywords = new()
    {
        new("SELECT", "Keyword", "Query data from tables", "Retrieves rows from a database"),
        new("FROM", "Keyword", "Specify table source", "Specifies the table(s) to query"),
        new("WHERE", "Keyword", "Filter rows", "Filters rows based on a condition"),
        new("JOIN", "Keyword", "Combine tables", "Combines rows from two or more tables"),
        new("INNER JOIN", "Keyword", "Inner join tables", "Returns matching rows from both tables"),
        new("LEFT JOIN", "Keyword", "Left outer join", "Returns all rows from left table and matching rows from right"),
        new("RIGHT JOIN", "Keyword", "Right outer join", "Returns all rows from right table and matching rows from left"),
        new("FULL OUTER JOIN", "Keyword", "Full outer join", "Returns all rows when there's a match in either table"),
        new("ON", "Keyword", "Join condition", "Specifies join condition"),
        new("GROUP BY", "Keyword", "Group rows", "Groups rows that have the same values"),
        new("HAVING", "Keyword", "Filter groups", "Filters grouped rows"),
        new("ORDER BY", "Keyword", "Sort results", "Sorts the result set"),
        new("INSERT INTO", "Keyword", "Insert data", "Inserts new rows into a table"),
        new("UPDATE", "Keyword", "Update data", "Modifies existing rows"),
        new("DELETE", "Keyword", "Delete data", "Removes rows from a table"),
        new("CREATE TABLE", "Keyword", "Create table", "Creates a new table"),
        new("ALTER TABLE", "Keyword", "Modify table", "Modifies an existing table"),
        new("DROP TABLE", "Keyword", "Delete table", "Deletes a table"),
        new("AS", "Keyword", "Alias", "Creates an alias for a column or table"),
        new("DISTINCT", "Keyword", "Unique values", "Returns only distinct values"),
        new("TOP", "Keyword", "Limit results", "Limits the number of rows returned"),
        new("CASE", "Keyword", "Conditional logic", "Evaluates conditions and returns a value"),
        new("WHEN", "Keyword", "Case condition", "Specifies a condition in a CASE statement"),
        new("THEN", "Keyword", "Case result", "Specifies the result for a WHEN condition"),
        new("ELSE", "Keyword", "Default result", "Specifies default result in CASE"),
        new("END", "Keyword", "End block", "Ends a CASE, BEGIN, or other block"),
        new("AND", "Keyword", "Logical AND", "Combines conditions with AND logic"),
        new("OR", "Keyword", "Logical OR", "Combines conditions with OR logic"),
        new("NOT", "Keyword", "Logical NOT", "Negates a condition"),
        new("IN", "Keyword", "Match list", "Checks if value matches any in a list"),
        new("BETWEEN", "Keyword", "Range check", "Checks if value is within a range"),
        new("LIKE", "Keyword", "Pattern match", "Matches a pattern using wildcards"),
        new("IS NULL", "Keyword", "Check for NULL", "Checks if value is NULL"),
        new("IS NOT NULL", "Keyword", "Check not NULL", "Checks if value is not NULL"),
        new("EXISTS", "Keyword", "Subquery exists", "Checks if subquery returns rows"),
        new("COUNT", "Keyword", "Count rows", "Returns the number of rows"),
        new("SUM", "Keyword", "Sum values", "Returns the sum of values"),
        new("AVG", "Keyword", "Average value", "Returns the average value"),
        new("MIN", "Keyword", "Minimum value", "Returns the minimum value"),
        new("MAX", "Keyword", "Maximum value", "Returns the maximum value"),
        new("UNION", "Keyword", "Combine results", "Combines result sets"),
        new("UNION ALL", "Keyword", "Combine all results", "Combines result sets including duplicates"),
        new("WITH", "Keyword", "Common table expression", "Defines a temporary named result set"),
        new("BEGIN", "Keyword", "Begin block", "Begins a transaction or code block"),
        new("COMMIT", "Keyword", "Commit transaction", "Commits the current transaction"),
        new("ROLLBACK", "Keyword", "Rollback transaction", "Rolls back the current transaction"),
        new("EXEC", "Keyword", "Execute procedure", "Executes a stored procedure"),
        new("EXECUTE", "Keyword", "Execute procedure", "Executes a stored procedure"),
    };

    // Common SQL snippet templates
    private static readonly List<SnippetCompletionItem> SqlSnippets = new()
    {
        new("sel", "SELECT statement", "Basic SELECT query", "SELECT ${1:columns} FROM ${2:table}"),
        new("selw", "SELECT with WHERE", "SELECT query with WHERE clause", "SELECT ${1:columns} FROM ${2:table} WHERE ${3:condition}"),
        new("selt", "SELECT TOP", "SELECT TOP N rows", "SELECT TOP ${1:100} ${2:columns} FROM ${3:table}"),
        new("join", "INNER JOIN", "INNER JOIN template", "INNER JOIN ${1:table} ON ${2:condition}"),
        new("leftjoin", "LEFT JOIN", "LEFT JOIN template", "LEFT JOIN ${1:table} ON ${2:condition}"),
        new("case", "CASE statement", "CASE WHEN template", "CASE WHEN ${1:condition} THEN ${2:result} ELSE ${3:default} END"),
        new("cte", "CTE (WITH)", "Common table expression", "WITH ${1:CTE_Name} AS (\n\tSELECT ${2:columns}\n\tFROM ${3:table}\n)\nSELECT * FROM ${1:CTE_Name}"),
    };

    public IntelliSenseProvider(IMetadataCache metadataCache)
    {
        _metadataCache = metadataCache;
    }

    public async Task<IntelliSenseData> GetCompletionDataAsync(string envId)
    {
        // Check completion cache first
        if (_completionCache.TryGetValue(envId, out var cached))
        {
            return cached;
        }

        // Fetch metadata from shared cache (which will fetch from DB if needed)
        var tables = await _metadataCache.GetTablesAsync(envId);
        var views = await _metadataCache.GetViewsAsync(envId);
        var storedProcs = await _metadataCache.GetStoredProceduresAsync(envId);

        // Get all function types
        var scalarFuncs = await _metadataCache.GetScalarFunctionsAsync(envId);
        var tableFuncs = await _metadataCache.GetTableValuedFunctionsAsync(envId);
        var aggFuncs = await _metadataCache.GetAggregateFunctionsAsync(envId);
        var allFunctions = scalarFuncs.Concat(tableFuncs).Concat(aggFuncs).ToList();

        // Convert to completion items
        var tableItems = tables.Select(t => new CompletionItem(
            $"{t.SchemaName}.{t.ObjectName}",
            "Table",
            $"Table: {t.SchemaName}.{t.ObjectName}",
            null)).ToList();

        var viewItems = views.Select(v => new CompletionItem(
            $"{v.SchemaName}.{v.ObjectName}",
            "View",
            $"View: {v.SchemaName}.{v.ObjectName}",
            null)).ToList();

        var procItems = storedProcs.Select(sp => new CompletionItem(
            $"{sp.SchemaName}.{sp.ObjectName}",
            "StoredProcedure",
            $"Stored Procedure: {sp.SchemaName}.{sp.ObjectName}",
            null)).ToList();

        var funcItems = allFunctions.Select(f => new CompletionItem(
            $"{f.SchemaName}.{f.ObjectName}",
            "Function",
            $"Function: {f.SchemaName}.{f.ObjectName}",
            null)).ToList();

        var data = new IntelliSenseData(
            SqlKeywords,
            tableItems,
            viewItems,
            procItems,
            funcItems,
            SqlSnippets);

        // Cache the completion data
        _completionCache[envId] = data;

        return data;
    }

    public async Task<List<ColumnCompletionItem>> GetTableColumnsAsync(string envId, string tableName)
    {
        try
        {
            // Parse schema.table format
            var parts = tableName.Split('.');
            string schemaName = parts.Length > 1 ? parts[0] : "dbo";
            string objectName = parts.Length > 1 ? parts[1] : parts[0];

            // Try as table first (uses shared cache)
            var columns = await _metadataCache.GetTableColumnsAsync(envId, schemaName, objectName);

            // If no results, try as view
            if (columns.Count == 0)
            {
                columns = await _metadataCache.GetViewColumnsAsync(envId, schemaName, objectName);
            }

            return columns.Select(c => new ColumnCompletionItem(
                c.Name,
                c.DataType,
                c.IsNullable,
                c.IsPrimaryKey,
                $"{c.DataType}{(c.MaxLength != null ? $"({c.MaxLength})" : "")} {(c.IsNullable ? "NULL" : "NOT NULL")}{(c.IsPrimaryKey ? " PK" : "")}"))
                .ToList();
        }
        catch
        {
            // Return empty list on error
            return new List<ColumnCompletionItem>();
        }
    }

    public void ClearCache(string envId)
    {
        _completionCache.TryRemove(envId, out _);
        _metadataCache.InvalidateEnvironment(envId);
    }

    public void ClearAllCache()
    {
        _completionCache.Clear();
        _metadataCache.InvalidateAll();
    }
}
