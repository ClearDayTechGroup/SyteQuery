namespace SyteQuery.Utilities;

/// <summary>
/// Utility for parsing TreeNode payload strings into structured components.
/// Payload formats:
/// - Environment root: "env:{id}"
/// - Environment folder: "env:{id}|folder:{kind}"
/// - Schema folder: "env:{id}|schema-{objectType}:{schemaName}"
/// - Table/View: "table:{schema}.{name}" or "view:{schema}.{name}"
/// - Stored Procedure: "sp:{schema}.{name}"
/// - Function: "function:{schema}.{name}"
/// - Trigger: "trigger:{schema}.{table}.{name}"
/// - Env-qualified objects: "env:{id}|table:{schema}.{name}" (same prefixes)
/// - Column: "column:{schema}.{table}.{name}"
/// </summary>
public static class TreeNodePayloadParser
{
    /// <summary>
    /// Parses an environment root payload: "env:{id}"
    /// </summary>
    public static bool TryParseEnvId(string payload, out string envId)
    {
        envId = "";

        if (!payload.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
            return false;

        // root must be exactly "env:{id}" (no folder suffix)
        if (payload.Contains('|'))
            return false;

        envId = payload.Substring("env:".Length);
        return !string.IsNullOrWhiteSpace(envId);
    }

    /// <summary>
    /// Parses an environment folder payload: "env:{id}|folder:{kind}"
    /// </summary>
    public static bool TryParseEnvFolder(string payload, out string envId, out string kind)
    {
        envId = "";
        kind = "";

        // "env:{id}|folder:{kind}"
        if (!payload.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = payload.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return false;

        if (!parts[0].StartsWith("env:", StringComparison.OrdinalIgnoreCase))
            return false;

        envId = parts[0].Substring("env:".Length);

        if (!parts[1].StartsWith("folder:", StringComparison.OrdinalIgnoreCase))
            return false;

        kind = parts[1].Substring("folder:".Length);

        return !string.IsNullOrWhiteSpace(envId) && !string.IsNullOrWhiteSpace(kind);
    }

    /// <summary>
    /// Parses a schema folder payload: "env:{id}|schema-{objectType}:{schemaName}"
    /// </summary>
    public static bool TryParseSchemaFolder(string? payload, out string envId, out string objectType, out string schemaName)
    {
        envId = "";
        objectType = "";
        schemaName = "";

        if (string.IsNullOrWhiteSpace(payload))
            return false;

        if (!payload.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = payload.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return false;

        envId = parts[0].Substring("env:".Length);

        if (!parts[1].StartsWith("schema-", StringComparison.OrdinalIgnoreCase))
            return false;

        var schemaPart = parts[1].Substring("schema-".Length);
        var colonIndex = schemaPart.IndexOf(':');

        if (colonIndex < 0)
            return false;

        objectType = schemaPart.Substring(0, colonIndex);
        schemaName = schemaPart.Substring(colonIndex + 1);

        return !string.IsNullOrWhiteSpace(envId) &&
               !string.IsNullOrWhiteSpace(objectType) &&
               !string.IsNullOrWhiteSpace(schemaName);
    }

    /// <summary>
    /// Parses a table or view payload: "table:{schema}.{name}" or "view:{schema}.{name}"
    /// </summary>
    public static bool TryParseTableOrView(string? payload, out string objectType, out string fullObjectName)
    {
        objectType = "";
        fullObjectName = "";

        if (string.IsNullOrWhiteSpace(payload))
            return false;

        // Format: "table:schema.name" or "view:schema.name"
        if (payload.StartsWith("table:", StringComparison.OrdinalIgnoreCase))
        {
            objectType = "table";
            fullObjectName = payload.Substring("table:".Length);
            return !string.IsNullOrWhiteSpace(fullObjectName);
        }

        if (payload.StartsWith("view:", StringComparison.OrdinalIgnoreCase))
        {
            objectType = "view";
            fullObjectName = payload.Substring("view:".Length);
            return !string.IsNullOrWhiteSpace(fullObjectName);
        }

        return false;
    }

    /// <summary>
    /// Parses an env-qualified object payload: "env:{id}|table:{schema}.{name}" or "env:{id}|sp:{schema}.{name}"
    /// Falls back to non-env payloads when env prefix is missing.
    /// </summary>
    public static bool TryParseEnvQualifiedObject(string? payload, out string envId, out string objectType, out string fullObjectName)
    {
        envId = "";
        objectType = "";
        fullObjectName = "";

        if (string.IsNullOrWhiteSpace(payload))
            return false;

        var parts = payload.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var objectPart = payload;

        if (parts.Length >= 2 && parts[0].StartsWith("env:", StringComparison.OrdinalIgnoreCase))
        {
            envId = parts[0].Substring("env:".Length);
            objectPart = parts[1];
        }

        if (objectPart.StartsWith("table:", StringComparison.OrdinalIgnoreCase))
        {
            objectType = "table";
            fullObjectName = objectPart.Substring("table:".Length);
            return !string.IsNullOrWhiteSpace(fullObjectName);
        }

        if (objectPart.StartsWith("view:", StringComparison.OrdinalIgnoreCase))
        {
            objectType = "view";
            fullObjectName = objectPart.Substring("view:".Length);
            return !string.IsNullOrWhiteSpace(fullObjectName);
        }

        if (objectPart.StartsWith("sp:", StringComparison.OrdinalIgnoreCase))
        {
            objectType = "sp";
            fullObjectName = objectPart.Substring("sp:".Length);
            return !string.IsNullOrWhiteSpace(fullObjectName);
        }

        if (objectPart.StartsWith("function:", StringComparison.OrdinalIgnoreCase))
        {
            objectType = "function";
            fullObjectName = objectPart.Substring("function:".Length);
            return !string.IsNullOrWhiteSpace(fullObjectName);
        }

        if (objectPart.StartsWith("trigger:", StringComparison.OrdinalIgnoreCase))
        {
            objectType = "trigger";
            fullObjectName = objectPart.Substring("trigger:".Length);
            return !string.IsNullOrWhiteSpace(fullObjectName);
        }

        return false;
    }

    /// <summary>
    /// Splits a full object name into schema and object parts
    /// </summary>
    public static bool TrySplitObjectName(string fullObjectName, out string schemaName, out string objectName)
    {
        schemaName = "";
        objectName = "";

        var parts = fullObjectName.Split('.');
        if (parts.Length != 2)
            return false;

        schemaName = parts[0];
        objectName = parts[1];

        return !string.IsNullOrWhiteSpace(schemaName) && !string.IsNullOrWhiteSpace(objectName);
    }
}
