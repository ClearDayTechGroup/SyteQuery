using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AvalonDock.Layout;
using AvalonDock.Themes;
using Microsoft.Extensions.DependencyInjection;
using SyteQuery.Desktop.Compare;
using SyteQuery.Desktop.Environments;
using SyteQuery.Desktop.History;
using SyteQuery.Desktop.ObjectExplorer;
using SyteQuery.Desktop.QueryEditor;
using SyteQuery.Desktop.Results;
using SyteQuery.Desktop.Snippets;
using SyteQuery.Desktop.Theming;
using SyteQuery.Desktop.Updates;
using SyteQuery.Features.Environments.Services;
using SyteQuery.Features.Metadata.Services;
using SyteQuery.Features.ObjectExplorer.Models;
using SyteQuery.Features.ObjectExplorer.Services;
using SyteQuery.Features.QueryEditor.Models;
using SyteQuery.Utilities;

namespace SyteQuery.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    // Only tables the user created (the ue_ prefix is the SyteLine customization convention)
    // can be scripted - same rule as the web version.
    private const string DefaultAccessAsPrefix = "ue_";

    private readonly IServiceProvider _services;
    private readonly IEnvironmentSessionManager _envMgr;
    private readonly IMetadataCache _metadataCache;
    private readonly IObjectDefinitionService _definitions;
    private readonly UpdateService _updates;

    private QueryEditorView? _activeQueryView;
    private QueryEditorViewModel? _activeQueryEditor;
    private int _queryCounter;

    public ObjectExplorerViewModel ObjectExplorerViewModel { get; }
    public ResultsGridViewModel ResultsGridViewModel { get; }
    public SnippetsPanelViewModel SnippetsPanelViewModel { get; }
    public HistoryPanelViewModel HistoryPanelViewModel { get; }

    /// <summary>Background metadata loading - its StatusText/Fraction drive the status bar.</summary>
    public MetadataPreloadCoordinator Preload { get; }

    /// <summary>The query tab currently in front - the shared Messages panel binds to this,
    /// and the Results panel shows its last result.</summary>
    public QueryEditorViewModel? ActiveQueryEditor
    {
        get => _activeQueryEditor;
        private set
        {
            if (ReferenceEquals(_activeQueryEditor, value)) return;
            _activeQueryEditor = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveQueryEditor)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow(
        ObjectExplorerViewModel objectExplorerViewModel,
        ResultsGridViewModel resultsGridViewModel,
        SnippetsPanelViewModel snippetsPanelViewModel,
        HistoryPanelViewModel historyPanelViewModel,
        MetadataPreloadCoordinator preloadCoordinator,
        UpdateService updateService,
        IEnvironmentSessionManager envMgr,
        IMetadataCache metadataCache,
        IObjectDefinitionService definitions,
        IServiceProvider services)
    {
        ObjectExplorerViewModel = objectExplorerViewModel;
        ResultsGridViewModel = resultsGridViewModel;
        SnippetsPanelViewModel = snippetsPanelViewModel;
        HistoryPanelViewModel = historyPanelViewModel;
        Preload = preloadCoordinator;
        _updates = updateService;
        _envMgr = envMgr;
        _metadataCache = metadataCache;
        _definitions = definitions;
        _services = services;
        InitializeComponent();
        DataContext = this;

        ApplyDockTheme();
        ThemeService.ThemeChanged += ApplyDockTheme;
        UpdateThemeMenuChecks();

        DockManager.ActiveContentChanged += (_, _) =>
        {
            // Only query tabs count - clicking Results/Object Explorer shouldn't change which
            // editor the shared panels are tied to.
            if (DockManager.ActiveContent is QueryEditorView view)
                SetActiveQueryView(view);
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                OpenQueryTab();
            }
        };

        ObjectExplorerViewModel.NodeSelected += node => _ = HandleNodeSelectedAsync(node);
        SnippetsPanelViewModel.LoadRequested += sql =>
        {
            if (_activeQueryView is null)
                OpenQueryTab(sql: sql);
            else
                _activeQueryView.SetQueryText(sql);
        };

        HistoryPanelViewModel.LoadRequested += entry =>
        {
            // Put the query in the current tab (or a new one if none is open), and switch to the
            // environment it ran against when we can find it again - history stores the name.
            var envId = _envMgr.Profiles.FirstOrDefault(p => p.Name == entry.EnvironmentName)?.Id;
            if (_activeQueryView is null)
            {
                OpenQueryTab(envId: envId, sql: entry.Query);
                return;
            }

            if (envId != null)
                _activeQueryEditor?.SetActiveEnvironment(envId);
            _activeQueryView.SetQueryText(entry.Query);
        };

        OpenQueryTab();

        // Warm every environment's object lists (tables/views/procs/functions) in the background so the
        // first expand of each folder is instant. Columns load per table when expanded.
        Preload.StartPreloadAll();
    }

    /// <summary>AvalonDock draws its own chrome (tabs, headers, splitters), so it needs an
    /// explicit light/dark theme to match the Fluent theme the rest of the window follows.</summary>
    private void ApplyDockTheme() =>
        DockManager.Theme = ThemeService.IsDark ? new Vs2013DarkTheme() : new Vs2013LightTheme();

    // ------------------------------------------------------------
    // Query tabs
    // ------------------------------------------------------------

    /// <summary>
    /// Opens a new query tab. Each tab gets its own QueryEditorViewModel (its own environment
    /// selection, messages and last result) - like SSMS query windows, and what the web
    /// version's "open in new tab" object actions relied on.
    /// </summary>
    private (QueryEditorView View, QueryEditorViewModel Model) OpenQueryTab(string? title = null, string? envId = null, string? sql = null)
    {
        var model = _services.GetRequiredService<QueryEditorViewModel>();

        var env = envId ?? ActiveQueryEditor?.SelectedEnvironmentId;
        if (env != null)
            model.SetActiveEnvironment(env);

        model.ResultsReady += result =>
        {
            if (ReferenceEquals(model, ActiveQueryEditor))
                ResultsGridViewModel.SetResults(result);
        };
        model.SnippetSaved += () => _ = SnippetsPanelViewModel.RefreshAsync();
        model.HistoryRecorded += () => _ = HistoryPanelViewModel.RefreshAsync();

        var view = new QueryEditorView { DataContext = model };
        if (sql != null)
            view.SetQueryText(sql);

        var document = new LayoutDocument { Title = title ?? $"Query {++_queryCounter}", Content = view };
        document.Closed += (_, _) =>
        {
            model.Dispose();
            if (ReferenceEquals(model, ActiveQueryEditor))
                SetActiveQueryView(null);
        };

        DocumentPane.Children.Add(document);
        document.IsActive = true;
        SetActiveQueryView(view);

        return (view, model);
    }

    private void SetActiveQueryView(QueryEditorView? view)
    {
        if (ReferenceEquals(_activeQueryView, view))
            return;

        _activeQueryView = view;
        ActiveQueryEditor = view?.Model;
        ResultsGridViewModel.SetResults(ActiveQueryEditor?.LastResult);
    }

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tag } && Enum.TryParse<ThemeChoice>(tag, out var choice))
            ThemeService.SetChoice(choice);

        UpdateThemeMenuChecks();
    }

    /// <summary>The three theme items act as radio buttons - a click on an already-checked item
    /// would otherwise toggle it off.</summary>
    private void UpdateThemeMenuChecks()
    {
        ThemeSystemItem.IsChecked = ThemeService.Choice == ThemeChoice.System;
        ThemeLightItem.IsChecked = ThemeService.Choice == ThemeChoice.Light;
        ThemeDarkItem.IsChecked = ThemeService.Choice == ThemeChoice.Dark;
    }

    private void OnNewQueryClick(object sender, RoutedEventArgs e) => OpenQueryTab();

    // ------------------------------------------------------------
    // Menu
    // ------------------------------------------------------------

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            $"SyteQuery {typeof(MainWindow).Assembly.GetName().Version?.ToString(3)}\nA native SyteLine query tool.\n\nBuilt by ClearDay Tech Group.",
            "About SyteQuery",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void OnCheckForUpdatesClick(object sender, RoutedEventArgs e) =>
        await _updates.CheckForUpdatesAsync(this);

    private void OnEnvironmentsClick(object sender, RoutedEventArgs e)
    {
        // Add/Edit/Remove all go through IEnvironmentSessionManager.ProfilesChanged, which
        // ObjectExplorerViewModel and every QueryEditorViewModel subscribe to directly.
        // Snapshot what matters for cached metadata, so after the dialog we can tell which
        // environments were added, removed or had their connection changed.
        var before = _envMgr.Profiles.ToDictionary(p => p.Id, p => (p.Url, p.Config, p.User));

        var dialog = new EnvironmentsWindow(_envMgr, _metadataCache) { Owner = this };
        dialog.ShowDialog();

        var after = _envMgr.Profiles.ToDictionary(p => p.Id, p => (p.Url, p.Config, p.User));
        foreach (var removedId in before.Keys.Except(after.Keys))
            Preload.Forget(removedId);

        foreach (var (id, connection) in after)
        {
            if (before.TryGetValue(id, out var old) && old != connection)
            {
                // Pointed at a different server/config/user - what we cached is for something else.
                ObjectExplorerViewModel.ResetEnvironment(id);
                Preload.Refresh(id);
            }
        }

        // Anything new (or never loaded) gets its object lists loaded in the background.
        Preload.StartPreloadForNew();
    }

    // ------------------------------------------------------------
    // Object Explorer
    // ------------------------------------------------------------

    private async void OnObjectExplorerItemExpanded(object sender, RoutedEventArgs e)
    {
        // Expanding is the only thing this does: IsExpanded is two-way bound, and
        // EnsureChildrenLoadedAsync never writes it back, so there's no feedback loop.
        if (e.OriginalSource is TreeViewItem { DataContext: TreeNode node })
        {
            await ObjectExplorerViewModel.EnsureChildrenLoadedAsync(node);
        }
    }

    /// <summary>
    /// A click on a row's label runs its action (like clicking the text in the web version);
    /// the expand chevron is a ToggleButton and is skipped so expanding never pops a dialog.
    /// Uses mouse-up rather than the tree's Selected event so clicking the same row again
    /// (e.g. after cancelling a prompt) works too - Selected only fires when selection changes.
    /// </summary>
    private void OnObjectExplorerMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        var current = e.OriginalSource as DependencyObject;
        while (current is not null and not TreeViewItem)
        {
            if (current is ToggleButton)
                return;

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        if (current is TreeViewItem { DataContext: TreeNode node })
            ObjectExplorerViewModel.SelectNode(node);
    }

    /// <summary>
    /// A real refresh: throws away the cached object lists, columns, triggers and IntelliSense data for
    /// the selected environment (or all of them if nothing is selected), collapses it in the tree, and
    /// reloads the object lists from the server in the background (columns and triggers reload per table
    /// when you expand it - preloading all of them took too long on a real environment). Before this the button only redrew the tree from the
    /// same cached data, so a schema change in SyteLine needed an app restart to show up.
    /// </summary>
    private void OnObjectExplorerRefreshClick(object sender, RoutedEventArgs e)
    {
        var selected = ObjectExplorerViewModel.SelectedEnvironmentId;
        var targets = selected is not null && _envMgr.Profiles.Any(p => p.Id == selected)
            ? new[] { selected }
            : _envMgr.Profiles.Select(p => p.Id).ToArray();

        if (targets.Length == 0)
        {
            ObjectExplorerViewModel.RefreshRoots();
            return;
        }

        foreach (var id in targets)
        {
            ObjectExplorerViewModel.ResetEnvironment(id);
            Preload.Refresh(id);
        }
    }

    /// <summary>
    /// Ported from the old QueryTool.HandleNodeSelectedAsync: stored procedures ask
    /// Show Definition / Compare, tables ask Quick Select / Script Table, views quick-select,
    /// functions and triggers open their definition - each in a new tab.
    /// </summary>
    private async Task HandleNodeSelectedAsync(TreeNode node)
    {
        try
        {
            if (node.Payload is null)
                return;

            if (TreeNodePayloadParser.TryParseEnvQualifiedObject(node.Payload, out var envId, out var objectType, out var fullName))
            {
                switch (objectType)
                {
                    case "sp":
                        await HandleStoredProcedureAsync(envId, fullName);
                        break;
                    case "function":
                        await OpenDefinitionTabAsync(envId, fullName, "function",
                            () => _definitions.GetFunctionDefinitionAsync(envId, fullName));
                        break;
                    case "trigger":
                        // Payload is schema.table.trigger but OBJECT_DEFINITION wants schema.trigger.
                        var parts = fullName.Split('.');
                        var triggerName = parts.Length == 3 ? $"{parts[0]}.{parts[2]}" : fullName;
                        await OpenDefinitionTabAsync(envId, triggerName, "trigger",
                            () => _definitions.GetTriggerDefinitionAsync(envId, triggerName));
                        break;
                    case "table":
                        await HandleTableAsync(envId, fullName);
                        break;
                    case "view":
                        OpenQueryTab(fullName, envId, $"SELECT TOP 100 * FROM {fullName};");
                        break;
                }

                return;
            }

            if (TreeNodePayloadParser.TryParseEnvId(node.Payload, out var rootEnvId))
                ActiveQueryEditor?.SetActiveEnvironment(rootEnvId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"That action failed: {ex.Message}", "SyteQuery", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private Task HandleStoredProcedureAsync(string envId, string procName)
    {
        var choice = ChoiceWindow.Ask(this, "Stored Procedure", $"Select an action for {procName}:", "Show Definition", "Compare");

        if (choice == 0)
        {
            return OpenDefinitionTabAsync(envId, procName, "stored procedure",
                () => _definitions.GetStoredProcedureDefinitionAsync(envId, procName));
        }

        if (choice == 1)
        {
            if (_envMgr.Profiles.Count < 2)
            {
                MessageBox.Show("Compare requires at least two environments.", "Compare Stored Procedure",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return Task.CompletedTask;
            }

            new CompareStoredProcedureWindow(_definitions, _envMgr.Profiles, procName, envId) { Owner = this }.Show();
        }

        return Task.CompletedTask;
    }

    private Task HandleTableAsync(string envId, string fullName)
    {
        var choice = ChoiceWindow.Ask(this, "Table Action", $"Select an action for {fullName}:", "Quick Select", "Script Table");

        if (choice == 0)
        {
            OpenQueryTab(fullName, envId, $"SELECT TOP 100 * FROM {fullName};");
            return Task.CompletedTask;
        }

        if (choice == 1)
        {
            if (!TreeNodePayloadParser.TrySplitObjectName(fullName, out var schemaName, out var tableName))
            {
                MessageBox.Show($"Invalid table name format: {fullName}", "SyteQuery", MessageBoxButton.OK, MessageBoxImage.Warning);
                return Task.CompletedTask;
            }

            if (!tableName.StartsWith(DefaultAccessAsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Vendor tables cannot be scripted.", "Script Table", MessageBoxButton.OK, MessageBoxImage.Information);
                return Task.CompletedTask;
            }

            return OpenDefinitionTabAsync(envId, fullName, "table",
                () => _definitions.GetTableScriptAsync(envId, schemaName, tableName));
        }

        return Task.CompletedTask;
    }

    /// <summary>Opens a new tab immediately (showing "-- Loading...") and fills it in when the
    /// definition arrives, like LoadDatabaseObjectDefinitionInNewTabAsync did.</summary>
    private async Task OpenDefinitionTabAsync(string envId, string title, string objectType, Func<Task<ObjectDefinitionResult>> fetch)
    {
        var (view, model) = OpenQueryTab(title, envId, "-- Loading...");
        model.AddMessage(QueryMessageLevel.Info, $"Loading {objectType}: {title}");

        ObjectDefinitionResult result;
        try
        {
            result = await fetch();
        }
        catch (Exception ex)
        {
            result = new ObjectDefinitionResult(false, null, ex.Message);
        }

        if (result.Success && !string.IsNullOrWhiteSpace(result.Definition))
        {
            view.SetQueryText(result.Definition);
            model.AddMessage(QueryMessageLevel.Info, $"Loaded {objectType}: {title}");
        }
        else
        {
            var error = result.ErrorMessage ?? "Definition not available";
            view.SetQueryText($"-- {error}");
            model.AddMessage(QueryMessageLevel.Warning, error);
        }
    }
}
