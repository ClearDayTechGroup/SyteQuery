using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SyteQuery.Features.Environments.Services;
using SyteQuery.Features.Metadata.Models;
using SyteQuery.Features.Metadata.Services;
using SyteQuery.Features.ObjectExplorer.Models;
using SyteQuery.Features.Notifications.Services;
using SyteQuery.Utilities;

namespace SyteQuery.Desktop.ObjectExplorer;

/// <summary>
/// Ported from the old Blazor ObjectExplorer.razor.cs - the lazy-load/search logic is
/// unchanged, only the "tell the UI to redraw" mechanism differs: Blazor's StateHasChanged()
/// calls are gone because TreeNode/ObservableCollection already push updates to WPF's
/// TreeView via INotifyPropertyChanged/INotifyCollectionChanged. EnsureConnectedAsync (the
/// old "is this environment's session open" check) was dropped - REST v2 is stateless
/// (see Phase 1), there's no session to ensure is open anymore.
/// </summary>
public sealed class ObjectExplorerViewModel : INotifyPropertyChanged
{
    private readonly IEnvironmentSessionManager _envMgr;
    private readonly IMetadataRepository _metadataRepo;
    private readonly IMetadataCache _metadataCache;
    private readonly INotificationService _notifications;

    private string _searchText = "";
    private bool _useStartsWith;
    private string? _statusMessage;

    public ObservableCollection<TreeNode> Roots { get; } = new();

    /// <summary>Raised when a leaf/object node is selected (single-click). Phase 4c (query
    /// editor) is the real consumer of this - it needs to switch the active environment and
    /// load the object's definition/columns into the editor. For now, nothing subscribes.</summary>
    public event Action<TreeNode>? NodeSelected;

    public ObjectExplorerViewModel(
        IEnvironmentSessionManager envMgr,
        IMetadataRepository metadataRepo,
        IMetadataCache metadataCache,
        INotificationService notifications)
    {
        _envMgr = envMgr;
        _metadataRepo = metadataRepo;
        _metadataCache = metadataCache;
        _notifications = notifications;

        _envMgr.ProfilesChanged += OnProfilesChanged;
        BuildEnvironmentRoots();
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                FilterNodes();
        }
    }

    public bool UseStartsWith
    {
        get => _useStartsWith;
        set
        {
            if (SetField(ref _useStartsWith, value))
                FilterNodes();
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    private void OnProfilesChanged() => BuildEnvironmentRoots();

    /// <summary>Payloads all start "env:{id}" optionally followed by "|..." - pull out the id.</summary>
    private static string? ExtractEnvironmentId(string? payload)
    {
        if (payload is null || !payload.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
            return null;

        var end = payload.IndexOf('|');
        var id = end < 0 ? payload["env:".Length..] : payload["env:".Length..end];
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    public void RefreshRoots() => BuildEnvironmentRoots();

    private void BuildEnvironmentRoots()
    {
        Roots.Clear();

        foreach (var env in _envMgr.Profiles)
        {
            Roots.Add(new TreeNode
            {
                Text = env.Name,
                Icon = "🗄️",
                NodeType = DbNodeType.Root,
                Payload = $"env:{env.Id}"
            });
        }

        if (Roots.Count == 0)
        {
            Roots.Add(new TreeNode
            {
                Text = "(no environments configured)",
                Icon = "ℹ️",
                NodeType = DbNodeType.Folder,
                Payload = "info:noenv"
            });
        }
    }

    // ------------------------------------------------------------
    // Expand/select dispatch
    // ------------------------------------------------------------

    /// <summary>
    /// Call when a node is expanded (by the user, or programmatically) - loads its
    /// children on first expand only. Deliberately doesn't touch node.IsExpanded itself:
    /// the XAML's IsExpanded binding is one-way (ViewModel -> View only, for search's
    /// auto-expand), so there's nothing to reconcile here and no feedback loop risk.
    /// </summary>
    public async Task EnsureChildrenLoadedAsync(TreeNode node)
    {
        if (node.HasLoadedChildren)
            return;

        if (TreeNodePayloadParser.TryParseEnvQualifiedObject(node.Payload, out var objectEnvId, out var objectType, out var fullObjectName)
            && (objectType == "table" || objectType == "view"))
        {
            if (objectType == "table")
                await LoadTableDetailsAsync(node, objectEnvId, fullObjectName);
            else
                await LoadViewDetailsAsync(node, objectEnvId, fullObjectName);
            return;
        }

        if (TreeNodePayloadParser.TryParseSchemaFolder(node.Payload, out var schemaEnvId, out var schemaType, out var schemaName))
        {
            await LoadSchemaChildrenAsync(node, schemaEnvId, schemaType, schemaName);
            return;
        }

        // Env-folder must be checked before env-root (prevents infinite folder recursion)
        if (node.Payload != null && TreeNodePayloadParser.TryParseEnvFolder(node.Payload, out var envId, out var folderKind))
        {
            switch (folderKind.ToLowerInvariant())
            {
                case "tables": await LoadTablesAsync(node, envId); break;
                case "views": await LoadViewsAsync(node, envId); break;
                case "sps": await LoadStoredProceduresAsync(node, envId); break;
                case "functions": await LoadFunctionsAsync(node, envId); break;
                case "functions-scalar": await LoadFunctionsByTypeAsync(node, envId, "scalar"); break;
                case "functions-table": await LoadFunctionsByTypeAsync(node, envId, "table"); break;
                case "functions-aggregate": await LoadFunctionsByTypeAsync(node, envId, "aggregate"); break;
            }
            return;
        }

        if (node.Payload != null && TreeNodePayloadParser.TryParseEnvId(node.Payload, out var rootEnvId))
        {
            await LoadEnvRootAsync(node, rootEnvId);
        }
    }

    /// <summary>The environment the last-clicked node belongs to (null if nothing selected) - what
    /// the Refresh button acts on, like SSMS refreshing the selected server.</summary>
    public string? SelectedEnvironmentId { get; private set; }

    /// <summary>Collapses an environment back to its initial, unloaded state so the next expand
    /// re-reads everything (from a freshly invalidated cache) instead of showing stale children.</summary>
    public void ResetEnvironment(string envId)
    {
        var root = Roots.FirstOrDefault(r => r.Payload == $"env:{envId}");
        if (root is null)
            return;

        root.IsExpanded = false;
        root.Children.Clear();
        root.HasLoadedChildren = false;
    }

    public void SelectNode(TreeNode node)
    {
        SelectedEnvironmentId = ExtractEnvironmentId(node.Payload) ?? SelectedEnvironmentId;
        StatusMessage = node.Payload;
        NodeSelected?.Invoke(node);
    }

    // ------------------------------------------------------------
    // Loaders
    // ------------------------------------------------------------

    private Task LoadEnvRootAsync(TreeNode envRoot, string envId)
    {
        envRoot.Children.Clear();
        envRoot.Children.Add(new TreeNode { Text = "Tables", Icon = "📁", NodeType = DbNodeType.Folder, Payload = $"env:{envId}|folder:tables" });
        envRoot.Children.Add(new TreeNode { Text = "Views", Icon = "📁", NodeType = DbNodeType.Folder, Payload = $"env:{envId}|folder:views" });
        envRoot.Children.Add(new TreeNode { Text = "Stored Procedures", Icon = "📁", NodeType = DbNodeType.Folder, Payload = $"env:{envId}|folder:sps" });
        envRoot.Children.Add(new TreeNode { Text = "Functions", Icon = "📁", NodeType = DbNodeType.Folder, Payload = $"env:{envId}|folder:functions" });
        envRoot.HasLoadedChildren = true;
        return Task.CompletedTask;
    }

    private Task LoadTablesAsync(TreeNode folder, string envId) =>
        LoadObjectsGroupedBySchemaAsync(folder, envId, () => _metadataCache.GetTablesAsync(envId), "tables", "🧾", DbNodeType.Table, "table");

    private Task LoadViewsAsync(TreeNode folder, string envId) =>
        LoadObjectsGroupedBySchemaAsync(folder, envId, () => _metadataCache.GetViewsAsync(envId), "views", "👁️", DbNodeType.View, "view");

    private Task LoadStoredProceduresAsync(TreeNode folder, string envId) =>
        LoadObjectsGroupedBySchemaAsync(folder, envId, () => _metadataCache.GetStoredProceduresAsync(envId), "stored procedures", "⚙️", DbNodeType.StoredProcedure, "sp");

    private Task LoadFunctionsAsync(TreeNode folder, string envId)
    {
        folder.Children.Clear();
        folder.Children.Add(new TreeNode { Text = "Scalar-valued Functions", Icon = "📁", NodeType = DbNodeType.Folder, Payload = $"env:{envId}|folder:functions-scalar" });
        folder.Children.Add(new TreeNode { Text = "Table-valued Functions", Icon = "📁", NodeType = DbNodeType.Folder, Payload = $"env:{envId}|folder:functions-table" });
        folder.Children.Add(new TreeNode { Text = "Aggregate Functions", Icon = "📁", NodeType = DbNodeType.Folder, Payload = $"env:{envId}|folder:functions-aggregate" });
        folder.HasLoadedChildren = true;
        return Task.CompletedTask;
    }

    private Task LoadFunctionsByTypeAsync(TreeNode folder, string envId, string funcType)
    {
        Func<Task<List<DatabaseObject>>> fetch = funcType switch
        {
            "scalar" => () => _metadataCache.GetScalarFunctionsAsync(envId),
            "table" => () => _metadataCache.GetTableValuedFunctionsAsync(envId),
            _ => () => _metadataCache.GetAggregateFunctionsAsync(envId)
        };

        return LoadObjectsGroupedBySchemaAsync(folder, envId, fetch, $"{funcType} functions", "ƒ", DbNodeType.Function, "function");
    }

    private async Task LoadObjectsGroupedBySchemaAsync(
        TreeNode folder,
        string envId,
        Func<Task<List<DatabaseObject>>> fetchObjects,
        string objectTypeName,
        string icon,
        DbNodeType nodeType,
        string payloadPrefix)
    {
        folder.IsLoading = true;
        try
        {
            var rows = await fetchObjects();
            folder.Children.Clear();

            if (rows.Count == 0)
            {
                folder.Children.Add(new TreeNode { Text = $"(no {objectTypeName} found)", Icon = "ℹ️", NodeType = DbNodeType.Folder, Payload = "info:empty" });
                folder.HasLoadedChildren = true;
                return;
            }

            var bySchema = rows
                .GroupBy(r => r.SchemaName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var schemaGroup in bySchema)
            {
                var schemaNode = new TreeNode
                {
                    Text = $"{schemaGroup.Key} ({schemaGroup.Count()})",
                    Icon = "📁",
                    NodeType = DbNodeType.SchemaFolder,
                    Payload = $"env:{envId}|schema-{payloadPrefix}s:{schemaGroup.Key}"
                };

                foreach (var obj in schemaGroup.OrderBy(x => x.ObjectName, StringComparer.OrdinalIgnoreCase))
                {
                    schemaNode.Children.Add(new TreeNode
                    {
                        Text = obj.ObjectName,
                        Icon = icon,
                        NodeType = nodeType,
                        Payload = $"env:{envId}|{payloadPrefix}:{schemaGroup.Key}.{obj.ObjectName}"
                    });
                }
                schemaNode.HasLoadedChildren = true;
                folder.Children.Add(schemaNode);
            }

            folder.HasLoadedChildren = true;
        }
        catch (Exception ex)
        {
            _notifications.ShowError($"Failed to load {objectTypeName}: {ex.Message}");
            StatusMessage = $"Failed to load {objectTypeName}: {ex.Message}";
        }
        finally
        {
            folder.IsLoading = false;
        }
    }

    private async Task LoadSchemaChildrenAsync(TreeNode schemaNode, string envId, string objectType, string schemaName)
    {
        schemaNode.IsLoading = true;
        try
        {
            string iconType;
            DbNodeType nodeType;
            List<DatabaseObject> rows;

            switch (objectType)
            {
                case "tables":
                    rows = await _metadataRepo.GetTablesBySchemaAsync(envId, schemaName);
                    iconType = "🧾"; nodeType = DbNodeType.Table; break;
                case "views":
                    rows = await _metadataRepo.GetViewsBySchemaAsync(envId, schemaName);
                    iconType = "👁️"; nodeType = DbNodeType.View; break;
                case "sps":
                    rows = await _metadataRepo.GetStoredProceduresBySchemaAsync(envId, schemaName);
                    iconType = "⚙️"; nodeType = DbNodeType.StoredProcedure; break;
                case "functions-scalar":
                    rows = await _metadataRepo.GetScalarFunctionsBySchemaAsync(envId, schemaName);
                    iconType = "ƒ"; nodeType = DbNodeType.Function; break;
                case "functions-table":
                    rows = await _metadataRepo.GetTableValuedFunctionsBySchemaAsync(envId, schemaName);
                    iconType = "ƒ"; nodeType = DbNodeType.Function; break;
                default:
                    rows = await _metadataRepo.GetAggregateFunctionsBySchemaAsync(envId, schemaName);
                    iconType = "ƒ"; nodeType = DbNodeType.Function; break;
            }

            schemaNode.Children.Clear();

            foreach (var obj in rows)
            {
                var payloadPrefix = objectType.StartsWith("functions-") ? "function" : objectType.TrimEnd('s');
                schemaNode.Children.Add(new TreeNode
                {
                    Text = obj.ObjectName,
                    Icon = iconType,
                    NodeType = nodeType,
                    Payload = $"env:{envId}|{payloadPrefix}:{schemaName}.{obj.ObjectName}"
                });
            }

            schemaNode.HasLoadedChildren = true;
        }
        catch (Exception ex)
        {
            _notifications.ShowError($"Failed to load {objectType}: {ex.Message}");
            StatusMessage = $"Failed to load {objectType}: {ex.Message}";
        }
        finally
        {
            schemaNode.IsLoading = false;
        }
    }

    private async Task LoadTableDetailsAsync(TreeNode tableNode, string envId, string fullObjectName)
    {
        tableNode.IsLoading = true;
        try
        {
            if (!TreeNodePayloadParser.TrySplitObjectName(fullObjectName, out var schemaName, out var tableName))
            {
                _notifications.ShowError($"Invalid table name format: {fullObjectName}");
                return;
            }

            tableNode.Children.Clear();

            var columnsFolder = new TreeNode { Text = "Columns", Icon = "📁", NodeType = DbNodeType.Folder, Payload = $"table-columns:{fullObjectName}|env:{envId}" };
            tableNode.Children.Add(columnsFolder);
            await LoadColumnsAsync(columnsFolder, envId, schemaName, tableName);

            var triggersFolder = new TreeNode { Text = "Triggers", Icon = "📁", NodeType = DbNodeType.Folder, Payload = $"table-triggers:{fullObjectName}|env:{envId}" };
            tableNode.Children.Add(triggersFolder);
            await LoadTriggersAsync(triggersFolder, envId, schemaName, tableName);

            tableNode.HasLoadedChildren = true;
        }
        catch (Exception ex)
        {
            _notifications.ShowError($"Failed to load table details: {ex.Message}");
            StatusMessage = $"Failed to load table details: {ex.Message}";
        }
        finally
        {
            tableNode.IsLoading = false;
        }
    }

    private async Task LoadViewDetailsAsync(TreeNode viewNode, string envId, string fullObjectName)
    {
        viewNode.IsLoading = true;
        try
        {
            if (!TreeNodePayloadParser.TrySplitObjectName(fullObjectName, out var schemaName, out var viewName))
            {
                _notifications.ShowError($"Invalid view name format: {fullObjectName}");
                return;
            }

            viewNode.Children.Clear();

            var columnsFolder = new TreeNode { Text = "Columns", Icon = "📁", NodeType = DbNodeType.Folder, Payload = $"view-columns:{fullObjectName}|env:{envId}" };
            viewNode.Children.Add(columnsFolder);
            await LoadViewColumnsAsync(columnsFolder, envId, schemaName, viewName);

            viewNode.HasLoadedChildren = true;
        }
        catch (Exception ex)
        {
            _notifications.ShowError($"Failed to load view details: {ex.Message}");
            StatusMessage = $"Failed to load view details: {ex.Message}";
        }
        finally
        {
            viewNode.IsLoading = false;
        }
    }

    private async Task LoadColumnsAsync(TreeNode columnsFolder, string envId, string schemaName, string tableName)
    {
        columnsFolder.IsLoading = true;
        try
        {
            var rows = await _metadataCache.GetTableColumnsAsync(envId, schemaName, tableName);
            columnsFolder.Children.Clear();

            if (rows.Count == 0)
            {
                columnsFolder.Children.Add(new TreeNode { Text = "(no columns found)", Icon = "ℹ️", NodeType = DbNodeType.Folder });
                columnsFolder.HasLoadedChildren = true;
                return;
            }

            foreach (var col in rows)
            {
                var typeInfo = col.DataType;
                if (!string.IsNullOrWhiteSpace(col.MaxLength))
                    typeInfo += $"({col.MaxLength})";

                var nullable = col.IsNullable ? " NULL" : " NOT NULL";
                var pkIndicator = col.IsPrimaryKey ? " 🔑" : "";

                columnsFolder.Children.Add(new TreeNode
                {
                    Text = $"{col.Name} ({typeInfo}{nullable}{pkIndicator})",
                    Icon = col.IsPrimaryKey ? "🔑" : "📌",
                    NodeType = DbNodeType.Column,
                    Payload = $"column:{schemaName}.{tableName}.{col.Name}"
                });
            }

            columnsFolder.HasLoadedChildren = true;
        }
        catch (Exception ex)
        {
            _notifications.ShowError($"Failed to load columns: {ex.Message}");
            StatusMessage = $"Failed to load columns: {ex.Message}";
        }
        finally
        {
            columnsFolder.IsLoading = false;
        }
    }

    private async Task LoadViewColumnsAsync(TreeNode columnsFolder, string envId, string schemaName, string viewName)
    {
        columnsFolder.IsLoading = true;
        try
        {
            var rows = await _metadataCache.GetViewColumnsAsync(envId, schemaName, viewName);
            columnsFolder.Children.Clear();

            if (rows.Count == 0)
            {
                columnsFolder.Children.Add(new TreeNode { Text = "(no columns found)", Icon = "ℹ️", NodeType = DbNodeType.Folder });
                columnsFolder.HasLoadedChildren = true;
                return;
            }

            foreach (var col in rows)
            {
                var typeInfo = col.DataType;
                if (!string.IsNullOrWhiteSpace(col.MaxLength))
                    typeInfo += $"({col.MaxLength})";

                var nullable = col.IsNullable ? " NULL" : " NOT NULL";

                columnsFolder.Children.Add(new TreeNode
                {
                    Text = $"{col.Name} ({typeInfo}{nullable})",
                    Icon = "📌",
                    NodeType = DbNodeType.Column,
                    Payload = $"column:{schemaName}.{viewName}.{col.Name}"
                });
            }

            columnsFolder.HasLoadedChildren = true;
        }
        catch (Exception ex)
        {
            _notifications.ShowError($"Failed to load view columns: {ex.Message}");
            StatusMessage = $"Failed to load view columns: {ex.Message}";
        }
        finally
        {
            columnsFolder.IsLoading = false;
        }
    }

    private async Task LoadTriggersAsync(TreeNode triggersFolder, string envId, string schemaName, string tableName)
    {
        triggersFolder.IsLoading = true;
        try
        {
            var rows = await _metadataCache.GetTableTriggersAsync(envId, schemaName, tableName);
            triggersFolder.Children.Clear();

            if (rows.Count == 0)
            {
                triggersFolder.Children.Add(new TreeNode { Text = "(no triggers found)", Icon = "ℹ️", NodeType = DbNodeType.Folder });
                triggersFolder.HasLoadedChildren = true;
                return;
            }

            foreach (var trigger in rows)
            {
                var status = trigger.IsEnabled ? "Enabled" : "Disabled";
                triggersFolder.Children.Add(new TreeNode
                {
                    Text = $"{trigger.Name} ({status})",
                    Icon = trigger.IsEnabled ? "⚡" : "🚫",
                    NodeType = DbNodeType.Trigger,
                    Payload = $"env:{envId}|trigger:{schemaName}.{tableName}.{trigger.Name}"
                });
            }

            triggersFolder.HasLoadedChildren = true;
        }
        catch (Exception ex)
        {
            _notifications.ShowError($"Failed to load triggers: {ex.Message}");
            StatusMessage = $"Failed to load triggers: {ex.Message}";
        }
        finally
        {
            triggersFolder.IsLoading = false;
        }
    }

    // ------------------------------------------------------------
    // Search / filtering (client-side, over already-loaded nodes only -
    // same limitation the Blazor version had)
    // ------------------------------------------------------------

    public void ClearSearch() => SearchText = "";

    private void FilterNodes()
    {
        if (string.IsNullOrWhiteSpace(_searchText))
            ShowAllNodes(Roots);
        else
            FilterNodesRecursive(Roots, _searchText.ToLowerInvariant());
    }

    private static void ShowAllNodes(IEnumerable<TreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsVisible = true;
            if (node.Children.Count > 0)
                ShowAllNodes(node.Children);
        }
    }

    private bool FilterNodesRecursive(IEnumerable<TreeNode> nodes, string searchLower)
    {
        bool anyVisible = false;

        foreach (var node in nodes)
        {
            var nodeMatches = _useStartsWith
                ? node.Text.StartsWith(searchLower, StringComparison.OrdinalIgnoreCase)
                : node.Text.Contains(searchLower, StringComparison.OrdinalIgnoreCase);

            var childrenVisible = node.Children.Count > 0 && FilterNodesRecursive(node.Children, searchLower);

            node.IsVisible = nodeMatches || childrenVisible;

            if (node.IsVisible)
            {
                anyVisible = true;
                if (childrenVisible && !node.IsExpanded)
                    node.IsExpanded = true;
            }
        }

        return anyVisible;
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
