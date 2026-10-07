using SyteQuery.Features.Metadata.Models;
using SyteQuery.Features.DatabaseQuery.Services;
using SyteQuery.Features.QueryEditor.Models;
using SyteQuery.Utilities;

namespace SyteQuery.Features.Metadata.Services;

/// <summary>
/// Repository for database metadata queries using SQL Server system catalogs
/// </summary>
public class MetadataRepository : IMetadataRepository
{
    private readonly IdoQueryService _queryService;

    public MetadataRepository(IdoQueryService queryService)
    {
        _queryService = queryService;
    }

    public async Task<List<DatabaseObject>> GetTablesAsync(string envId)
    {
        const string sql = """
            select
                table_schema as [schema_name],
                table_name   as [object_name]
            from information_schema.tables
            where table_type = 'BASE TABLE'
            order by table_schema, table_name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "retrieve tables");
        return MapToDatabaseObjects(result.Rows);
    }

    public async Task<List<DatabaseObject>> GetViewsAsync(string envId)
    {
        const string sql = """
            select
                s.name as [schema_name],
                v.name as [object_name]
            from sys.views v
            join sys.schemas s on s.schema_id = v.schema_id
            where s.name not in ('sys', 'INFORMATION_SCHEMA')
            order by s.name, v.name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "retrieve views");
        return MapToDatabaseObjects(result.Rows);
    }

    public async Task<List<DatabaseObject>> GetStoredProceduresAsync(string envId)
    {
        const string sql = """
            select
                s.name as [schema_name],
                p.name as [object_name]
            from sys.procedures p
            join sys.schemas s on s.schema_id = p.schema_id
            where s.name not in ('sys', 'INFORMATION_SCHEMA')
            order by s.name, p.name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "retrieve stored procedures");
        return MapToDatabaseObjects(result.Rows);
    }

    public async Task<List<DatabaseObject>> GetScalarFunctionsAsync(string envId)
    {
        const string sql = """
            select
                s.name as [schema_name],
                o.name as [object_name]
            from sys.objects o
            join sys.schemas s on s.schema_id = o.schema_id
            where o.type = 'FN'
              and s.name not in ('sys', 'INFORMATION_SCHEMA')
            order by s.name, o.name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "execute query");
        return MapToDatabaseObjects(result.Rows);
    }

    public async Task<List<DatabaseObject>> GetTableValuedFunctionsAsync(string envId)
    {
        const string sql = """
            select
                s.name as [schema_name],
                o.name as [object_name]
            from sys.objects o
            join sys.schemas s on s.schema_id = o.schema_id
            where o.type in ('IF', 'TF')
              and s.name not in ('sys', 'INFORMATION_SCHEMA')
            order by s.name, o.name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "execute query");
        return MapToDatabaseObjects(result.Rows);
    }

    public async Task<List<DatabaseObject>> GetAggregateFunctionsAsync(string envId)
    {
        const string sql = """
            select
                s.name as [schema_name],
                o.name as [object_name]
            from sys.objects o
            join sys.schemas s on s.schema_id = o.schema_id
            where o.type = 'AF'
              and s.name not in ('sys', 'INFORMATION_SCHEMA')
            order by s.name, o.name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "execute query");
        return MapToDatabaseObjects(result.Rows);
    }

    public async Task<List<DatabaseObject>> GetTablesBySchemaAsync(string envId, string schemaName)
    {
        var escapedSchema = SqlStringHelper.EscapeSqlString(schemaName);
        var sql = $"""
            select
                '{escapedSchema}' as [schema_name],
                table_name as [object_name]
            from information_schema.tables
            where table_type = 'BASE TABLE'
              and table_schema = '{escapedSchema}'
            order by table_name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "execute query");
        return MapToDatabaseObjects(result.Rows);
    }

    public async Task<List<DatabaseObject>> GetViewsBySchemaAsync(string envId, string schemaName)
    {
        var escapedSchema = SqlStringHelper.EscapeSqlString(schemaName);
        var sql = $"""
            select
                '{escapedSchema}' as [schema_name],
                v.name as [object_name]
            from sys.views v
            join sys.schemas s on s.schema_id = v.schema_id
            where s.name = '{escapedSchema}'
            order by v.name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "execute query");
        return MapToDatabaseObjects(result.Rows);
    }

    public async Task<List<DatabaseObject>> GetStoredProceduresBySchemaAsync(string envId, string schemaName)
    {
        var escapedSchema = SqlStringHelper.EscapeSqlString(schemaName);
        var sql = $"""
            select
                '{escapedSchema}' as [schema_name],
                p.name as [object_name]
            from sys.procedures p
            join sys.schemas s on s.schema_id = p.schema_id
            where s.name = '{escapedSchema}'
            order by p.name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "execute query");
        return MapToDatabaseObjects(result.Rows);
    }

    public async Task<List<DatabaseObject>> GetScalarFunctionsBySchemaAsync(string envId, string schemaName)
    {
        var escapedSchema = SqlStringHelper.EscapeSqlString(schemaName);
        var sql = $"""
            select
                '{escapedSchema}' as [schema_name],
                o.name as [object_name]
            from sys.objects o
            join sys.schemas s on s.schema_id = o.schema_id
            where o.type = 'FN'
              and s.name = '{escapedSchema}'
            order by o.name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "execute query");
        return MapToDatabaseObjects(result.Rows);
    }

    public async Task<List<DatabaseObject>> GetTableValuedFunctionsBySchemaAsync(string envId, string schemaName)
    {
        var escapedSchema = SqlStringHelper.EscapeSqlString(schemaName);
        var sql = $"""
            select
                '{escapedSchema}' as [schema_name],
                o.name as [object_name]
            from sys.objects o
            join sys.schemas s on s.schema_id = o.schema_id
            where o.type in ('IF', 'TF')
              and s.name = '{escapedSchema}'
            order by o.name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "execute query");
        return MapToDatabaseObjects(result.Rows);
    }

    public async Task<List<DatabaseObject>> GetAggregateFunctionsBySchemaAsync(string envId, string schemaName)
    {
        var escapedSchema = SqlStringHelper.EscapeSqlString(schemaName);
        var sql = $"""
            select
                '{escapedSchema}' as [schema_name],
                o.name as [object_name]
            from sys.objects o
            join sys.schemas s on s.schema_id = o.schema_id
            where o.type = 'AF'
              and s.name = '{escapedSchema}'
            order by o.name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "execute query");
        return MapToDatabaseObjects(result.Rows);
    }

    public async Task<List<ColumnInfo>> GetTableColumnsAsync(string envId, string schemaName, string tableName)
    {
        var (escapedSchema, escapedTable) = SqlStringHelper.EscapeObjectName(schemaName, tableName);
        var sql = $"""
            SELECT
                c.column_name,
                c.data_type,
                c.character_maximum_length,
                c.is_nullable,
                CASE
                    WHEN pk.column_name IS NOT NULL THEN 1
                    ELSE 0
                END AS is_primary_key
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT ku.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage ku
                    ON tc.constraint_name = ku.constraint_name
                    AND tc.table_schema = ku.table_schema
                    AND tc.table_name = ku.table_name
                WHERE tc.constraint_type = 'PRIMARY KEY'
                    AND tc.table_schema = '{escapedSchema}'
                    AND tc.table_name = '{escapedTable}'
            ) pk ON c.column_name = pk.column_name
            WHERE c.table_schema = '{escapedSchema}'
                AND c.table_name = '{escapedTable}'
            ORDER BY c.ordinal_position;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "execute query");
        return MapToColumnInfo(result.Rows);
    }

    public async Task<List<ColumnInfo>> GetViewColumnsAsync(string envId, string schemaName, string viewName)
    {
        var (escapedSchema, escapedView) = SqlStringHelper.EscapeObjectName(schemaName, viewName);
        var sql = $"""
            SELECT
                column_name,
                data_type,
                character_maximum_length,
                is_nullable,
                0 as is_primary_key
            FROM information_schema.columns
            WHERE table_schema = '{escapedSchema}'
                AND table_name = '{escapedView}'
            ORDER BY ordinal_position;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "execute query");
        return MapToColumnInfo(result.Rows);
    }

    public async Task<List<TriggerInfo>> GetTableTriggersAsync(string envId, string schemaName, string tableName)
    {
        var (escapedSchema, escapedTable) = SqlStringHelper.EscapeObjectName(schemaName, tableName);
        var sql = $"""
            SELECT
                t.name AS trigger_name,
                CASE
                    WHEN t.is_disabled = 1 THEN 0
                    ELSE 1
                END AS is_enabled
            FROM sys.triggers t
            INNER JOIN sys.tables tab ON t.parent_id = tab.object_id
            INNER JOIN sys.schemas s ON tab.schema_id = s.schema_id
            WHERE s.name = '{escapedSchema}'
                AND tab.name = '{escapedTable}'
            ORDER BY t.name;
            """;

        var result = await _queryService.ExecuteAsync(envId, sql);
        EnsureSuccess(result, "execute query");
        return MapToTriggerInfo(result.Rows);
    }

    // Helper methods to map dictionaries to strongly-typed models
    /// <summary>
    /// Checks query execution result and throws if failed
    /// </summary>
    private static void EnsureSuccess(QueryExecutionResult result, string operationName)
    {
        if (!result.Success)
            throw new InvalidOperationException(result.Message ?? $"Failed to {operationName}");
    }

    private static List<DatabaseObject> MapToDatabaseObjects(List<Dictionary<string, object?>>? rows)
    {
        if (rows == null || rows.Count == 0)
            return new List<DatabaseObject>();

        return rows
            .Select(row => new DatabaseObject(
                SchemaName: JsonValueExtractor.GetStringValue(row, "schema_name"),
                ObjectName: JsonValueExtractor.GetStringValue(row, "object_name")))
            .Where(obj => !string.IsNullOrWhiteSpace(obj.SchemaName) && !string.IsNullOrWhiteSpace(obj.ObjectName))
            .ToList();
    }

    private static List<ColumnInfo> MapToColumnInfo(List<Dictionary<string, object?>>? rows)
    {
        if (rows == null || rows.Count == 0)
            return new List<ColumnInfo>();

        return rows
            .Select(row => new ColumnInfo(
                Name: JsonValueExtractor.GetStringValue(row, "column_name"),
                DataType: JsonValueExtractor.GetStringValue(row, "data_type"),
                MaxLength: JsonValueExtractor.GetStringValue(row, "character_maximum_length"),
                IsNullable: JsonValueExtractor.GetStringValue(row, "is_nullable").Equals("YES", StringComparison.OrdinalIgnoreCase),
                IsPrimaryKey: JsonValueExtractor.GetStringValue(row, "is_primary_key") == "1"))
            .ToList();
    }

    private static List<TriggerInfo> MapToTriggerInfo(List<Dictionary<string, object?>>? rows)
    {
        if (rows == null || rows.Count == 0)
            return new List<TriggerInfo>();

        return rows
            .Select(row => new TriggerInfo(
                Name: JsonValueExtractor.GetStringValue(row, "trigger_name"),
                IsEnabled: JsonValueExtractor.GetStringValue(row, "is_enabled") == "1"))
            .ToList();
    }
}
