using System.Threading;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SyteQuery;
using SyteQuery.Desktop.History;
using SyteQuery.Desktop.ObjectExplorer;
using SyteQuery.Desktop.QueryEditor;
using SyteQuery.Desktop.Results;
using SyteQuery.Desktop.Snippets;
using SyteQuery.Features.Database.Data;

namespace SyteQuery.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = "SyteQuery.Desktop.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private IHost? _host;
    private IServiceScope? _rootScope;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply the saved light/dark choice before any window exists, so nothing flashes the wrong theme.
        SyteQuery.Desktop.Theming.ThemeService.ApplySavedChoice();

        // --- Single-instance lock ---
        _singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, out var isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("SyteQuery is already running.", "SyteQuery", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // --- Host + DI ---
        // Content root = the folder the exe lives in (not the working directory): an installed app can
        // be launched from a shortcut with any working directory, and appsettings.json sits beside the exe.
        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("DefaultConnection")
                    ?? ServiceCollectionExtensions.GetDefaultSqliteConnectionString();

                services.AddSyteQueryCore(context.Configuration, connectionString);
                // Scoped, not Singleton: these depend on Scoped services (IEnvironmentSessionManager,
                // etc.) via AddSyteQueryCore. There's only ever one scope for the app's whole
                // lifetime (see the remarks below), so this is equivalent in practice to a
                // singleton - but Singleton-depending-on-Scoped is a captive-dependency bug that
                // Host.CreateDefaultBuilder() only validates (and throws on) in the Development
                // environment, so it'd work today and break the moment DOTNET_ENVIRONMENT is set.
                services.AddScoped<MainWindow>();
                services.AddScoped<ObjectExplorerViewModel>();
                // Transient: every query tab gets its own (MainWindow resolves one per tab from the app scope).
                services.AddTransient<QueryEditorViewModel>();
                services.AddScoped<ResultsGridViewModel>();
                services.AddScoped<SnippetsPanelViewModel>();
                services.AddScoped<HistoryPanelViewModel>();
                services.AddScoped<MetadataPreloadCoordinator>();
                services.AddSingleton<SyteQuery.Desktop.Updates.UpdateService>();
            })
            .Build();

        // One scope for the app's lifetime - see ServiceCollectionExtensions' remarks
        // on why that's the right call for a single-user desktop app.
        _rootScope = _host.Services.CreateScope();

        // Migrate/seed first, before anything (including any hosted service the host starts below)
        // can touch the database and hit a missing schema.
        try
        {
            var context = _rootScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await SeedDatabase.InitializeAsync(context);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize the local database:\n\n{ex.Message}",
                "SyteQuery - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // Load configured environments into IEnvironmentSessionManager.Profiles - the old
        // Blazor version did this per-circuit (OnInitializedAsync); a desktop app just needs
        // it once, before anything (like ObjectExplorerViewModel, resolved next) reads Profiles.
        await _rootScope.ServiceProvider.GetRequiredService<SyteQuery.Features.Environments.Services.IEnvironmentSessionManager>().InitializeAsync();

        // Keep query history bounded (newest 1000, nothing older than 90 days) and give the space
        // back to the file if anything was removed. Best-effort - never block startup on it.
        try
        {
            await _rootScope.ServiceProvider.GetRequiredService<SyteQuery.Features.QueryHistory.Services.IQueryHistoryService>()
                .PruneAsync(vacuumIfRemoved: true);
        }
        catch
        {
            // History housekeeping failing is not worth a startup error.
        }

        var mainWindow = _rootScope.ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            _rootScope?.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }

        if (_singleInstanceMutex != null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
