using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SyteQuery.Features.DatabaseQuery.Services;
using SyteQuery.Features.Environments.Services;
using SyteQuery.Features.IntelliSense.Models;
using SyteQuery.Features.IntelliSense.Services;
using SyteQuery.Features.QueryEditor.Models;
using SyteQuery.Features.QueryEditor.Services;
using SyteQuery.Features.QueryHistory.Models;
using SyteQuery.Features.QueryHistory.Services;
using SyteQuery.Features.QuerySnippets.Services;
using SyteQuery.Features.SqlFormatting.Services;

namespace SyteQuery.Desktop.QueryEditor;

/// <summary>
/// Ported from the old Blazor QueryTool.razor.cs's execute/format logic - deliberately a
/// single-editor, single-environment slice (no multi-tab document management, no query
/// history/snippets integration yet) to keep 4c reviewable; those are candidates for a
/// later pass once this is confirmed working. Results are surfaced as plain text/messages
/// for now - a real virtualized grid is Phase 4d's job, not this one's.
/// </summary>
public sealed class QueryEditorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IEnvironmentSessionManager _envMgr;
    private readonly IdoQueryService _queryService;
    private readonly ISqlFormatter _formatter;
    private readonly IQueryAnalyzer _analyzer;
    private readonly IIntelliSenseProvider _intelliSense;
    private readonly IQueryHistoryService _history;

    private string? _selectedEnvironmentId;
    private string _statusText = "Ready";
    private string? _errorMessage;
    private bool _isBusy;

    public ObservableCollection<EnvProfile> Environments { get; } = new();
    public ObservableCollection<QueryMessage> Messages { get; } = new();

    /// <summary>Exposed directly (rather than wrapped) so QueryEditorView's code-behind can
    /// open a SnippetEditWindow for "Save as Snippet" without this ViewModel needing to know
    /// anything about that dialog - same shortcut MainWindow already takes for
    /// IEnvironmentSessionManager/IMetadataCache.</summary>
    public IQuerySnippetService SnippetService { get; }

    /// <summary>Fired after every execution attempt (success, failure, or exception) so the
    /// Results panel (a separate ViewModel/View - Phase 4d) can update.</summary>
    public event Action<QueryExecutionResult>? ResultsReady;

    /// <summary>The most recent execution result for this editor tab - lets the shared Results
    /// panel redraw when you switch back to a tab that already ran something.</summary>
    public QueryExecutionResult? LastResult { get; private set; }

    private void PublishResult(QueryExecutionResult result)
    {
        LastResult = result;
        ResultsReady?.Invoke(result);
    }

    /// <summary>Each query tab owns one of these now, so it has to stop listening to the
    /// (app-lifetime) environment manager when its tab closes.</summary>
    public void Dispose() => _envMgr.ProfilesChanged -= OnProfilesChanged;

    /// <summary>Fired after "Save as Snippet" succeeds, so the docked Snippets panel (a
    /// separate ViewModel) can refresh its list.</summary>
    public event Action? SnippetSaved;

    /// <summary>Fired after a run has been saved to history so the History panel can refresh.</summary>
    public event Action? HistoryRecorded;

    public QueryEditorViewModel(
        IEnvironmentSessionManager envMgr,
        IdoQueryService queryService,
        ISqlFormatter formatter,
        IQueryAnalyzer analyzer,
        IIntelliSenseProvider intelliSense,
        IQuerySnippetService snippetService,
        IQueryHistoryService history)
    {
        _envMgr = envMgr;
        _queryService = queryService;
        _formatter = formatter;
        _analyzer = analyzer;
        _intelliSense = intelliSense;
        SnippetService = snippetService;
        _history = history;

        _envMgr.ProfilesChanged += OnProfilesChanged;
        RefreshEnvironments();
    }

    public string? SelectedEnvironmentId
    {
        get => _selectedEnvironmentId;
        set => SetField(ref _selectedEnvironmentId, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    /// <summary>Set when the last run failed (or was blocked) - drives the red banner above the
    /// editor so a failure is impossible to miss without opening the Messages panel.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetField(ref _errorMessage, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public void DismissError() => ErrorMessage = null;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    private void OnProfilesChanged() => RefreshEnvironments();

    private void RefreshEnvironments()
    {
        var previouslySelected = _selectedEnvironmentId;
        Environments.Clear();
        foreach (var env in _envMgr.Profiles)
            Environments.Add(env);

        if (previouslySelected != null && Environments.Any(e => e.Id == previouslySelected))
            SelectedEnvironmentId = previouslySelected;
        else
            SelectedEnvironmentId = Environments.FirstOrDefault()?.Id;
    }

    /// <summary>Called from Object Explorer node selection to switch context without
    /// disturbing the query text (e.g. clicking an environment root or a folder).</summary>
    public void SetActiveEnvironment(string envId)
    {
        if (Environments.Any(e => e.Id == envId))
            SelectedEnvironmentId = envId;
    }

    /// <summary>Newest first, so the latest message is always the top one you see (same as the
    /// old UiState.AddMessage).</summary>
    public void AddMessage(QueryMessageLevel level, string text) =>
        Messages.Insert(0, new QueryMessage(DateTime.UtcNow, level, text));

    public void ClearMessages() => Messages.Clear();

    /// <summary>Called by QueryEditorView after SnippetEditWindow reports a successful
    /// save - see SnippetSaved's remarks.</summary>
    public void NotifySnippetSaved() => SnippetSaved?.Invoke();

    public string Format(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            AddMessage(QueryMessageLevel.Warning, "No query to format.");
            return sql;
        }

        try
        {
            var formatted = _formatter.Format(sql);
            AddMessage(QueryMessageLevel.Info, "Query formatted.");
            return formatted;
        }
        catch (Exception ex)
        {
            AddMessage(QueryMessageLevel.Error, $"Failed to format query: {ex.Message}");
            return sql;
        }
    }

    public async Task<IntelliSenseData?> GetCompletionDataAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedEnvironmentId))
            return null;

        try
        {
            return await _intelliSense.GetCompletionDataAsync(_selectedEnvironmentId);
        }
        catch
        {
            // IntelliSense is best-effort - a failure here shouldn't interrupt typing.
            return null;
        }
    }

    /// <summary>Context-aware column completion for "identifier." - identifier can be a
    /// bare name (schema defaults to dbo) or "schema.table"; returns empty for anything
    /// else (an alias, a CTE name, etc.) rather than guessing.</summary>
    public async Task<List<ColumnCompletionItem>> GetTableColumnsAsync(string tableName)
    {
        if (string.IsNullOrWhiteSpace(_selectedEnvironmentId) || string.IsNullOrWhiteSpace(tableName))
            return new List<ColumnCompletionItem>();

        try
        {
            return await _intelliSense.GetTableColumnsAsync(_selectedEnvironmentId, tableName);
        }
        catch
        {
            return new List<ColumnCompletionItem>();
        }
    }

    public async Task ExecuteAsync(string sql)
    {
        if (IsBusy)
            return;

        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(_selectedEnvironmentId))
        {
            AddMessage(QueryMessageLevel.Warning, "Select an environment before executing.");
            ErrorMessage = "Select an environment before executing.";
            return;
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            AddMessage(QueryMessageLevel.Warning, "No command to execute.");
            return;
        }

        if (SqlValidator.ContainsProhibitedDdl(sql))
        {
            AddMessage(QueryMessageLevel.Warning, "CREATE/ALTER/DROP PROCEDURE/VIEW/FUNCTION/TRIGGER statements are not allowed.");
            ErrorMessage = "Blocked: CREATE/ALTER/DROP PROCEDURE/VIEW/FUNCTION/TRIGGER statements are not allowed.";
            return;
        }

        var analysis = _analyzer.Analyze(sql);
        foreach (var warning in analysis.Warnings)
        {
            var level = warning.Severity switch
            {
                QueryWarningSeverity.Error => QueryMessageLevel.Error,
                QueryWarningSeverity.Warning => QueryMessageLevel.Warning,
                _ => QueryMessageLevel.Info
            };
            AddMessage(level, $"{warning.Message}: {warning.Details}");
        }

        var queryToExecute = sql;
        if (analysis.ShouldEnforceLimit && !string.IsNullOrWhiteSpace(analysis.ModifiedQuery))
        {
            queryToExecute = analysis.ModifiedQuery;
            AddMessage(QueryMessageLevel.Info, "Row limit automatically applied.");
        }

        IsBusy = true;
        StatusText = "Executing...";
        AddMessage(QueryMessageLevel.Info, "Executing query...");

        var sw = Stopwatch.StartNew();
        var succeeded = false;
        int? rowCount = null;
        string? errorMessage = null;
        try
        {
            var result = await _queryService.ExecuteAsync(_selectedEnvironmentId, queryToExecute, includeDebug: true);
            sw.Stop();

            if (!result.Success)
            {
                errorMessage = result.Message;
                AddMessage(QueryMessageLevel.Error, result.Message);
                StatusText = "Query failed";
                ErrorMessage = result.Message;
                PublishResult(result);
                return;
            }

            succeeded = true;
            rowCount = result.RowCount;
            AddMessage(QueryMessageLevel.Info, $"{result.Message} ({result.RowCount} row(s), {sw.ElapsedMilliseconds} ms)");
            StatusText = $"Query complete - {result.RowCount} row(s) in {sw.ElapsedMilliseconds} ms";
            PublishResult(result);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            AddMessage(QueryMessageLevel.Error, $"Execution failed: {ex.Message}");
            StatusText = "Query failed";
            ErrorMessage = ex.Message;
            PublishResult(new QueryExecutionResult { Success = false, Message = ex.Message });
        }
        finally
        {
            IsBusy = false;

            sw.Stop();
            await RecordHistoryAsync(sql, succeeded, rowCount, errorMessage, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>Saves the run to query history (what the user typed, not the row-limited rewrite).
    /// Best-effort: a history failure must never get in the way of running queries.</summary>
    private async Task RecordHistoryAsync(string sql, bool succeeded, int? rowCount, string? errorMessage, long durationMs)
    {
        try
        {
            var env = Environments.FirstOrDefault(e => e.Id == _selectedEnvironmentId);
            await _history.SaveAsync(new QueryHistoryEntry
            {
                Id = Guid.NewGuid().ToString(),
                Query = sql,
                ExecutedAt = DateTime.UtcNow,
                EnvironmentId = _selectedEnvironmentId ?? "",
                EnvironmentName = env?.Name,
                DurationMs = durationMs,
                RowCount = rowCount,
                Success = succeeded,
                ErrorMessage = errorMessage
            });
            HistoryRecorded?.Invoke();
        }
        catch
        {
            // Intentionally swallowed - see summary.
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
