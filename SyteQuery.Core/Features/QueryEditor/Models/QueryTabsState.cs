namespace SyteQuery.Features.QueryEditor.Models;

/// <summary>
/// Manages multiple query tabs state, similar to SSMS.
/// </summary>
public sealed class QueryTabsState
{
    private readonly List<QueryTab> _tabs = new();
    private QueryTab? _activeTab;

    public IReadOnlyList<QueryTab> Tabs => _tabs;
    public QueryTab? ActiveTab => _activeTab;

    public QueryTabsState()
    {
        // Start with one default tab
        var initialTab = new QueryTab("Query 1");
        _tabs.Add(initialTab);
        _activeTab = initialTab;
    }

    public QueryTab AddNewTab()
    {
        var newTab = new QueryTab();
        _tabs.Add(newTab);
        _activeTab = newTab;
        return newTab;
    }

    public void SetActiveTab(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab != null)
        {
            _activeTab = tab;
        }
    }

    public bool CloseTab(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab == null)
            return false;

        // Don't allow closing the last tab
        if (_tabs.Count == 1)
            return false;

        var index = _tabs.IndexOf(tab);
        _tabs.Remove(tab);

        // If closing active tab, activate adjacent tab
        if (_activeTab == tab)
        {
            // Activate the tab to the left, or the first tab if closing the first tab
            var newActiveIndex = index > 0 ? index - 1 : 0;
            _activeTab = _tabs[newActiveIndex];
        }

        return true;
    }

    public void UpdateTabContent(string tabId, string content)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab != null)
        {
            tab.SqlContent = content;
            tab.IsModified = true;
        }
    }

    public void MarkTabSaved(string tabId)
    {
        var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
        if (tab != null)
        {
            tab.IsModified = false;
        }
    }
}
