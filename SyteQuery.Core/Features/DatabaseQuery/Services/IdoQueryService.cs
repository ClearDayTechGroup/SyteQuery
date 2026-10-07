using SyteQuery.Features.Environments.Services;
using SyteQuery.Features.QueryEditor.Models;
using SyteQuery.Features.QueryEditor.Services;
using SyteQuery.Features.DataExport.Services;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;

namespace SyteQuery.Features.DatabaseQuery.Services;

public sealed class IdoQueryService
{
    private readonly IEnvironmentSessionManager _envMgr;

    public IdoQueryService(IEnvironmentSessionManager envMgr)
    {
        _envMgr = envMgr;
    }

    // ------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------

    public async Task<QueryExecutionResult> ExecuteAsync(
        string envId,
        string cmd,
        bool includeDebug = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(envId))
            throw new ArgumentException("Environment ID cannot be empty", nameof(envId));

        if (string.IsNullOrWhiteSpace(cmd))
            throw new ArgumentException("Command cannot be empty", nameof(cmd));

        var debugLog = includeDebug ? new StringBuilder() : null;

        try
        {
            debugLog?.AppendLine($"Environment: {envId}");
            debugLog?.AppendLine($"Command length: {cmd.Length}");

            // Execute via environment manager
            var result = await _envMgr.ExecuteAsync(envId, cmd, ct);

            debugLog?.AppendLine($"Success: {result.Ok}");
            debugLog?.AppendLine($"Message: {result.Message}");

            if (!result.Ok)
            {
                return new QueryExecutionResult
                {
                    Success = false,
                    Message = result.Message ?? "Query execution failed",
                    DebugInfo = debugLog?.ToString()
                };
            }

            // Transform raw data into structured result
            return ProcessRawData(result.DataBase64, result.Message, debugLog);
        }
        catch (Exception ex)
        {
            debugLog?.AppendLine($"Exception: {ex.GetType().Name} - {ex.Message}");

            return new QueryExecutionResult
            {
                Success = false,
                Message = $"Execution failed: {ex.Message}",
                DebugInfo = debugLog?.ToString()
            };
        }
    }

    // ------------------------------------------------------------
    // Data Transformation
    // ------------------------------------------------------------

    private QueryExecutionResult ProcessRawData(
        string? dataPayload,
        string? message,
        StringBuilder? debugLog)
    {
        debugLog?.AppendLine($"Raw payload length: {dataPayload?.Length ?? 0}");

        if (string.IsNullOrWhiteSpace(dataPayload))
        {
            // No data and an error-looking message: the SQL failed even though the call itself "succeeded".
            if (QueryErrorDetector.LooksLikeError(message))
            {
                return new QueryExecutionResult
                {
                    Success = false,
                    Message = message!,
                    DebugInfo = debugLog?.ToString()
                };
            }

            return new QueryExecutionResult
            {
                Success = true,
                Rows = null,
                Message = string.IsNullOrWhiteSpace(message) ? "No data returned" : message,
                DebugInfo = debugLog?.ToString()
            };
        }

        // Decode if Base64
        var json = DataEncoder.TryDecodeBase64ToUtf8(dataPayload) ?? dataPayload;

        var preview = json.Length > 80 ? json[..80] : json;
        debugLog?.AppendLine($"JSON preview: {preview}");

        // Deserialize
        List<Dictionary<string, object?>>? rows = null;
        try
        {
            rows = JsonConvert.DeserializeObject<List<Dictionary<string, object?>>>(json);
            debugLog?.AppendLine($"Deserialized {rows?.Count ?? 0} rows");
        }
        catch (JsonException ex)
        {
            debugLog?.AppendLine($"JSON deserialization failed: {ex.Message}");

            return new QueryExecutionResult
            {
                Success = false,
                Message = $"Failed to parse query results: {ex.Message}",
                DebugInfo = debugLog?.ToString()
            };
        }

        if ((rows is null || rows.Count == 0) && QueryErrorDetector.LooksLikeError(message))
        {
            return new QueryExecutionResult
            {
                Success = false,
                Message = message!,
                DebugInfo = debugLog?.ToString()
            };
        }

        return new QueryExecutionResult
        {
            Success = true,
            Rows = rows,
            Message = string.IsNullOrWhiteSpace(message) ? "Command complete" : message,
            DebugInfo = debugLog?.ToString()
        };
    }
}