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
    private readonly IdoVersionRegistry _idoVersions;

    public IdoQueryService(IEnvironmentSessionManager envMgr, IdoVersionRegistry idoVersions)
    {
        _envMgr = envMgr;
        _idoVersions = idoVersions;
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
            var processed = ProcessRawData(result.DataBase64, result.Message, debugLog);
            if (processed.IdoVersion is { } idoVersion)
                _idoVersions.Record(envId, idoVersion);
            return processed;
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

    /// <summary>
    /// Runs a script that was split into batches at GO lines, one call per batch, and combines the results:
    /// every batch's result sets, in order. Stops at the first batch that fails and returns what came before,
    /// like SSMS does. Each batch is its own call to the IDO, so - unlike in SSMS - temp tables and variables
    /// do not carry over from one batch to the next.
    /// </summary>
    public async Task<QueryExecutionResult> ExecuteBatchesAsync(
        string envId,
        IReadOnlyList<string> batches,
        bool includeDebug = false,
        CancellationToken ct = default)
    {
        if (batches.Count == 0)
            throw new ArgumentException("There is nothing to run.", nameof(batches));

        if (batches.Count == 1)
            return await ExecuteAsync(envId, batches[0], includeDebug, ct);

        var sets = new List<QueryResultSet>();
        QueryExecutionResult? last = null;

        for (var i = 0; i < batches.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var result = await ExecuteAsync(envId, batches[i], includeDebug, ct);
            last = result;
            sets.AddRange(result.ResultSets);

            if (!result.Success)
            {
                return new QueryExecutionResult
                {
                    Success = false,
                    Rows = sets.Count > 0 ? sets[0].Rows : null,
                    ResultSets = sets,
                    IdoVersion = result.IdoVersion,
                    Message = $"Batch {i + 1} of {batches.Count} failed: {result.Message}",
                    DebugInfo = result.DebugInfo
                };
            }
        }

        return new QueryExecutionResult
        {
            Success = true,
            Rows = sets.Count > 0 ? sets[0].Rows : null,
            ResultSets = sets,
            IdoVersion = last!.IdoVersion,
            Message = last.Message,
            DebugInfo = last.DebugInfo
        };
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

        if (debugLog is not null)
        {
            var json = DataEncoder.TryDecodeBase64ToUtf8(dataPayload) ?? dataPayload;
            debugLog.AppendLine($"JSON preview: {(json.Length > 80 ? json[..80] : json)}");
        }

        if (!IdoResultParser.TryParse(dataPayload, out var parsed, out var problem))
        {
            debugLog?.AppendLine($"Parsing failed: {problem}");

            return new QueryExecutionResult
            {
                Success = false,
                Message = problem ?? "Failed to parse query results.",
                DebugInfo = debugLog?.ToString()
            };
        }

        var sets = parsed!.ResultSets;
        var first = sets.Count > 0 ? sets[0] : null;
        debugLog?.AppendLine($"IDO format version {parsed.Version}; {sets.Count} result set(s), {sets.Sum(s => s.RowCount)} row(s)");

        // A later statement failed: the sets read before it are kept so they can still be shown.
        if (parsed.Error is not null)
        {
            return new QueryExecutionResult
            {
                Success = false,
                Rows = first?.Rows,
                ResultSets = sets,
                IdoVersion = parsed.Version,
                Message = parsed.Error,
                DebugInfo = debugLog?.ToString()
            };
        }

        if (sets.All(s => s.RowCount == 0) && QueryErrorDetector.LooksLikeError(message))
        {
            return new QueryExecutionResult
            {
                Success = false,
                IdoVersion = parsed.Version,
                Message = message!,
                DebugInfo = debugLog?.ToString()
            };
        }

        return new QueryExecutionResult
        {
            Success = true,
            Rows = first?.Rows,
            ResultSets = sets,
            IdoVersion = parsed.Version,
            Message = string.IsNullOrWhiteSpace(message) ? "Command complete" : message,
            DebugInfo = debugLog?.ToString()
        };
    }
}