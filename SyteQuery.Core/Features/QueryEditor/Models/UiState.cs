namespace SyteQuery.Features.QueryEditor.Models;

/// <summary>
/// Manages UI-related state for the QueryTool component.
/// </summary>
public sealed class UiState
{
    private readonly List<UiMessage> _messages = new();

    public bool IsBusy { get; private set; }
    public string Status { get; private set; } = "Ready";
    public string ActiveTab { get; private set; } = "results";
    public double EditorPct { get; set; } = 65;
    public bool IsExplorerCollapsed { get; set; } = false;
    public bool IsEnvironmentToolbarCollapsed { get; set; } = false;
    public bool IsIntelliSenseEnabled { get; set; } = true;
    public IReadOnlyList<UiMessage> Messages => _messages;

    public void SetBusy(bool busy) => IsBusy = busy;
    public void SetStatus(string status) => Status = status;
    public void SetActiveTab(string tab) => ActiveTab = tab;

    public void SetError(string message)
    {
        Status = "Error";
        AddMessage(message, MessageSeverity.Error);
    }

    public void AddMessage(string text, MessageSeverity severity = MessageSeverity.Info)
    {
        _messages.Insert(0, new UiMessage(DateTime.Now, text, severity));

        // Keep only last 250 messages
        if (_messages.Count > 250)
            _messages.RemoveRange(250, _messages.Count - 250);
    }

    public void ClearMessages()
    {
        _messages.Clear();
    }
}