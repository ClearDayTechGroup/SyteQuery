using System.Text;
using SyteQuery.Features.DatabaseQuery.Services;
using SyteQuery.Features.SqlFormatting.Services;
using SyteQuery.Utilities;

namespace SyteQuery.Features.ObjectExplorer.Services;

/// <summary>
/// Service for retrieving SQL object definitions (stored procedures, functions, views)
/// </summary>
public class ObjectDefinitionService : IObjectDefinitionService
{
    private readonly IdoQueryService _queryService;
    private readonly ISqlFormatter _sqlFormatter;

    public ObjectDefinitionService(IdoQueryService queryService, ISqlFormatter sqlFormatter)
    {
        _queryService = queryService;
        _sqlFormatter = sqlFormatter;
    }

    public async Task<ObjectDefinitionResult> GetStoredProcedureDefinitionAsync(string envId, string objectName)
    {
        return await GetObjectDefinitionAsync(envId, objectName, "stored procedure");
    }

    public async Task<ObjectDefinitionResult> GetFunctionDefinitionAsync(string envId, string objectName)
    {
        return await GetObjectDefinitionAsync(envId, objectName, "function");
    }

    public async Task<ObjectDefinitionResult> GetViewDefinitionAsync(string envId, string objectName)
    {
        return await GetObjectDefinitionAsync(envId, objectName, "view");
    }

    public async Task<ObjectDefinitionResult> GetTriggerDefinitionAsync(string envId, string objectName)
    {
        return await GetObjectDefinitionAsync(envId, objectName, "trigger");
    }

    public async Task<ObjectDefinitionResult> GetTableScriptAsync(string envId, string schemaName, string tableName)
    {
        try
        {
            var (escapedSchema, escapedTable) = SqlStringHelper.EscapeObjectName(schemaName, tableName);
            var fullName = $"{escapedSchema}.{escapedTable}";
            var objectIdExpr = $"OBJECT_ID('{escapedSchema}.{escapedTable}')";

            var columnsResult = await _queryService.ExecuteAsync(envId, $"""
                SELECT
                    c.name AS column_name,
                    t.name AS data_type,
                    c.max_length,
                    c.precision,
                    c.scale,
                    c.is_nullable,
                    c.is_identity,
                    c.is_computed,
                    dc.name AS default_name,
                    dc.definition AS default_definition,
                    cc.definition AS computed_definition
                FROM sys.columns c
                JOIN sys.types t ON c.user_type_id = t.user_type_id
                LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
                LEFT JOIN sys.computed_columns cc ON c.object_id = cc.object_id AND c.column_id = cc.column_id
                WHERE c.object_id = {objectIdExpr}
                ORDER BY c.column_id;
                """);

            if (!columnsResult.Success || columnsResult.Rows == null || columnsResult.Rows.Count == 0)
            {
                return new ObjectDefinitionResult(false, null, $"No table found with name: {schemaName}.{tableName}");
            }

            var pkResult = await _queryService.ExecuteAsync(envId, $"""
                SELECT
                    kc.name AS constraint_name,
                    i.type_desc AS index_type,
                    c.name AS column_name,
                    ic.key_ordinal,
                    ic.is_descending_key
                FROM sys.key_constraints kc
                JOIN sys.indexes i ON kc.parent_object_id = i.object_id AND kc.unique_index_id = i.index_id
                JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                WHERE kc.type = 'PK'
                  AND kc.parent_object_id = {objectIdExpr}
                ORDER BY ic.key_ordinal;
                """);

            var fkResult = await _queryService.ExecuteAsync(envId, $"""
                SELECT
                    fk.name AS fk_name,
                    sch_from.name AS from_schema,
                    t_from.name AS from_table,
                    c_from.name AS from_column,
                    sch_to.name AS to_schema,
                    t_to.name AS to_table,
                    c_to.name AS to_column,
                    fkc.constraint_column_id AS column_order,
                    fk.delete_referential_action_desc AS on_delete,
                    fk.update_referential_action_desc AS on_update
                FROM sys.foreign_keys fk
                JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                JOIN sys.tables t_from ON fk.parent_object_id = t_from.object_id
                JOIN sys.schemas sch_from ON t_from.schema_id = sch_from.schema_id
                JOIN sys.columns c_from ON fkc.parent_object_id = c_from.object_id AND fkc.parent_column_id = c_from.column_id
                JOIN sys.tables t_to ON fk.referenced_object_id = t_to.object_id
                JOIN sys.schemas sch_to ON t_to.schema_id = sch_to.schema_id
                JOIN sys.columns c_to ON fkc.referenced_object_id = c_to.object_id AND fkc.referenced_column_id = c_to.column_id
                WHERE fk.parent_object_id = {objectIdExpr}
                ORDER BY fk.name, fkc.constraint_column_id;
                """);

            var indexResult = await _queryService.ExecuteAsync(envId, $"""
                SELECT
                    i.name AS index_name,
                    i.is_unique,
                    i.type_desc,
                    c.name AS column_name,
                    ic.key_ordinal,
                    ic.is_descending_key,
                    ic.is_included_column
                FROM sys.indexes i
                JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                WHERE i.object_id = {objectIdExpr}
                  AND i.is_primary_key = 0
                  AND i.is_unique_constraint = 0
                  AND i.type_desc <> 'HEAP'
                ORDER BY i.name, ic.key_ordinal, ic.index_column_id;
                """);

            var triggerResult = await _queryService.ExecuteAsync(envId, $"""
                SELECT
                    t.name AS trigger_name,
                    OBJECT_DEFINITION(t.object_id) AS definition
                FROM sys.triggers t
                WHERE t.parent_id = {objectIdExpr}
                ORDER BY t.name;
                """);

            var sb = new StringBuilder();
            sb.AppendLine($"-- Script for [{schemaName}].[{tableName}]");
            sb.AppendLine();
            sb.AppendLine($"CREATE TABLE [{schemaName}].[{tableName}] (");

            var columnLines = new List<string>();
            foreach (var row in columnsResult.Rows)
            {
                var columnName = JsonValueExtractor.GetStringValue(row, "column_name");
                var dataType = JsonValueExtractor.GetStringValue(row, "data_type");
                var maxLength = GetIntValue(row, "max_length");
                var precision = GetIntValue(row, "precision");
                var scale = GetIntValue(row, "scale");
                var isNullable = GetIntValue(row, "is_nullable") == 1;
                var isIdentity = GetIntValue(row, "is_identity") == 1;
                var isComputed = GetIntValue(row, "is_computed") == 1;
                var defaultName = JsonValueExtractor.GetStringValue(row, "default_name");
                var defaultDefinition = JsonValueExtractor.GetStringValue(row, "default_definition");
                var computedDefinition = JsonValueExtractor.GetStringValue(row, "computed_definition");

                var line = new StringBuilder();
                line.Append($"    [{columnName}]");

                if (isComputed && !string.IsNullOrWhiteSpace(computedDefinition))
                {
                    line.Append(" AS ");
                    line.Append(computedDefinition.Trim());
                }
                else
                {
                    line.Append(' ');
                    line.Append(FormatDataType(dataType, maxLength, precision, scale));

                    if (isIdentity)
                    {
                        line.Append(" IDENTITY(1,1)");
                    }

                    line.Append(isNullable ? " NULL" : " NOT NULL");

                    if (!string.IsNullOrWhiteSpace(defaultDefinition))
                    {
                        if (!string.IsNullOrWhiteSpace(defaultName))
                        {
                            line.Append($" CONSTRAINT [{defaultName}]");
                        }
                        line.Append($" DEFAULT {defaultDefinition.Trim()}");
                    }
                }

                columnLines.Add(line.ToString());
            }

            if (pkResult.Success && pkResult.Rows != null && pkResult.Rows.Count > 0)
            {
                var pkName = JsonValueExtractor.GetStringValue(pkResult.Rows[0], "constraint_name");
                var indexType = JsonValueExtractor.GetStringValue(pkResult.Rows[0], "index_type");
                var pkColumns = pkResult.Rows
                    .OrderBy(r => GetIntValue(r, "key_ordinal"))
                    .Select(r =>
                    {
                        var col = JsonValueExtractor.GetStringValue(r, "column_name");
                        var desc = GetIntValue(r, "is_descending_key") == 1 ? " DESC" : " ASC";
                        return $"[{col}]{desc}";
                    });

                var pkLine = $"    CONSTRAINT [{pkName}] PRIMARY KEY {NormalizeIndexType(indexType)} ({string.Join(", ", pkColumns)})";
                columnLines.Add(pkLine);
            }

            sb.AppendLine(string.Join(",\n", columnLines));
            sb.AppendLine(");");

            if (fkResult.Success && fkResult.Rows != null && fkResult.Rows.Count > 0)
            {
                sb.AppendLine();
                var fkGroups = fkResult.Rows.GroupBy(r => JsonValueExtractor.GetStringValue(r, "fk_name"));
                foreach (var fkGroup in fkGroups)
                {
                    var rows = fkGroup.OrderBy(r => GetIntValue(r, "column_order")).ToList();
                    var fkName = fkGroup.Key;
                    var fromSchema = JsonValueExtractor.GetStringValue(rows[0], "from_schema");
                    var fromTable = JsonValueExtractor.GetStringValue(rows[0], "from_table");
                    var toSchema = JsonValueExtractor.GetStringValue(rows[0], "to_schema");
                    var toTable = JsonValueExtractor.GetStringValue(rows[0], "to_table");
                    var onDelete = JsonValueExtractor.GetStringValue(rows[0], "on_delete");
                    var onUpdate = JsonValueExtractor.GetStringValue(rows[0], "on_update");

                    var fromColumns = rows.Select(r => $"[{JsonValueExtractor.GetStringValue(r, "from_column")}]");
                    var toColumns = rows.Select(r => $"[{JsonValueExtractor.GetStringValue(r, "to_column")}]");

                    sb.Append($"ALTER TABLE [{fromSchema}].[{fromTable}] ADD CONSTRAINT [{fkName}] FOREIGN KEY ({string.Join(", ", fromColumns)}) ");
                    sb.Append($"REFERENCES [{toSchema}].[{toTable}] ({string.Join(", ", toColumns)})");

                    if (!string.Equals(onDelete, "NO_ACTION", StringComparison.OrdinalIgnoreCase))
                        sb.Append($" ON DELETE {onDelete}");
                    if (!string.Equals(onUpdate, "NO_ACTION", StringComparison.OrdinalIgnoreCase))
                        sb.Append($" ON UPDATE {onUpdate}");

                    sb.AppendLine(";");
                }
            }

            if (indexResult.Success && indexResult.Rows != null && indexResult.Rows.Count > 0)
            {
                sb.AppendLine();
                var indexGroups = indexResult.Rows.GroupBy(r => JsonValueExtractor.GetStringValue(r, "index_name"));
                foreach (var indexGroup in indexGroups)
                {
                    var rows = indexGroup.ToList();
                    var indexName = indexGroup.Key;
                    var isUnique = GetIntValue(rows[0], "is_unique") == 1;
                    var indexType = JsonValueExtractor.GetStringValue(rows[0], "type_desc");

                    var keyColumns = rows
                        .Where(r => GetIntValue(r, "is_included_column") == 0)
                        .OrderBy(r => GetIntValue(r, "key_ordinal"))
                        .Select(r =>
                        {
                            var col = JsonValueExtractor.GetStringValue(r, "column_name");
                            var desc = GetIntValue(r, "is_descending_key") == 1 ? " DESC" : " ASC";
                            return $"[{col}]{desc}";
                        })
                        .ToList();

                    var includeColumns = rows
                        .Where(r => GetIntValue(r, "is_included_column") == 1)
                        .Select(r => $"[{JsonValueExtractor.GetStringValue(r, "column_name")}]")
                        .Distinct()
                        .ToList();

                    var uniqueText = isUnique ? "UNIQUE " : "";
                    sb.Append($"CREATE {uniqueText}{NormalizeIndexType(indexType)} INDEX [{indexName}] ON [{schemaName}].[{tableName}] ({string.Join(", ", keyColumns)})");

                    if (includeColumns.Count > 0)
                        sb.Append($" INCLUDE ({string.Join(", ", includeColumns)})");

                    sb.AppendLine(";");
                }
            }

            if (triggerResult.Success && triggerResult.Rows != null && triggerResult.Rows.Count > 0)
            {
                foreach (var row in triggerResult.Rows)
                {
                    var definition = JsonValueExtractor.GetStringValue(row, "definition");
                    if (string.IsNullOrWhiteSpace(definition))
                        continue;

                    sb.AppendLine();
                    sb.AppendLine(definition.Trim());
                }
            }

            var script = sb.ToString();
            var formatted = _sqlFormatter.Format(script);
            return new ObjectDefinitionResult(true, formatted, null);
        }
        catch (Exception ex)
        {
            return new ObjectDefinitionResult(false, null, $"Error scripting table: {ex.Message}");
        }
    }

    private async Task<ObjectDefinitionResult> GetObjectDefinitionAsync(string envId, string objectName, string objectType)
    {
        try
        {
            var escaped = SqlStringHelper.EscapeSqlString(objectName);
            var sql = $"SELECT OBJECT_DEFINITION(OBJECT_ID('{escaped}')) AS [definition];";

            var result = await _queryService.ExecuteAsync(envId, sql);

            if (result.Rows == null || result.Rows.Count == 0)
            {
                return new ObjectDefinitionResult(false, null, $"No {objectType} found with name: {objectName}");
            }

            var row = result.Rows[0];
            if (!row.TryGetValue("definition", out var defValue) || defValue is null)
            {
                return new ObjectDefinitionResult(false, null, $"Could not retrieve definition for {objectType}: {objectName}");
            }

            var definition = JsonValueExtractor.GetStringValue(defValue);

            if (string.IsNullOrWhiteSpace(definition))
            {
                return new ObjectDefinitionResult(false, null, $"Definition is empty for {objectType}: {objectName}");
            }

            var formatted = _sqlFormatter.Format(definition);
            return new ObjectDefinitionResult(true, formatted, null);
        }
        catch (Exception ex)
        {
            return new ObjectDefinitionResult(false, null, $"Error loading {objectType} definition: {ex.Message}");
        }
    }

    private static int GetIntValue(Dictionary<string, object?> row, string key)
    {
        var value = JsonValueExtractor.GetStringValue(row, key);
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static string FormatDataType(string dataType, int maxLength, int precision, int scale)
    {
        var normalized = dataType.ToLowerInvariant();

        if (normalized is "varchar" or "char" or "varbinary" or "binary")
        {
            var length = maxLength == -1 ? "max" : maxLength.ToString();
            return $"{dataType}({length})";
        }

        if (normalized is "nvarchar" or "nchar")
        {
            var length = maxLength == -1 ? "max" : (maxLength / 2).ToString();
            return $"{dataType}({length})";
        }

        if (normalized is "decimal" or "numeric")
        {
            return $"{dataType}({precision},{scale})";
        }

        if (normalized is "datetime2" or "datetimeoffset" or "time")
        {
            return scale > 0 ? $"{dataType}({scale})" : dataType;
        }

        return dataType;
    }

    private static string NormalizeIndexType(string indexType)
    {
        if (string.IsNullOrWhiteSpace(indexType))
            return "NONCLUSTERED";

        if (indexType.Contains("CLUSTERED", StringComparison.OrdinalIgnoreCase))
            return indexType.ToUpperInvariant();

        return "NONCLUSTERED";
    }
}
