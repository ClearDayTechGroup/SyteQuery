using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SyteQuery.Features.Common;
using SyteQuery.Features.Common.Services;
using SyteQuery.Features.DataExport.Services;
using SyteQuery.Features.Database.Data;
using SyteQuery.Features.DatabaseQuery.Services;
using SyteQuery.Features.Environments.Repositories;
using SyteQuery.Features.Environments.Services;
using SyteQuery.Features.IntelliSense.Services;
using SyteQuery.Features.Metadata.Services;
using SyteQuery.Features.Notifications.Services;
using SyteQuery.Features.ObjectExplorer.Services;
using SyteQuery.Features.QueryEditor.Services;
using SyteQuery.Features.QueryHistory.Repositories;
using SyteQuery.Features.QueryHistory.Services;
using SyteQuery.Features.QuerySnippets.Repositories;
using SyteQuery.Features.QuerySnippets.Services;
using SyteQuery.Features.SqlFormatting.Services;

namespace SyteQuery;

/// <summary>
/// Registers every SyteQuery.Core service. Lives here (not in SyteQuery.Desktop) so any
/// future host - a test harness, a CLI, whatever - gets the same composition for free
/// instead of re-deriving it. This is a straight port of the old Blazor Program.cs's
/// registrations, minus anything web-specific (SignalR, cookie auth, static assets, the
/// razor component registrations) and IHttpContextAccessor (removed - see
/// BaseAuthenticatedService, it was dead weight even before this move).
///
/// Lifetime note: everything here uses the same Scoped/Singleton lifetimes the ASP.NET
/// Core version used, which assumes something creates one IServiceScope per unit of work.
/// A desktop app has no per-request boundary the way a web server does, so the composition
/// root (SyteQuery.Desktop's App.xaml.cs) creates a single scope for the app's lifetime and
/// resolves the UI-facing services from it. That's fine here specifically because anything
/// that needs a *fresh* short-lived scope for background/repeated work (DatabaseEnvironment-
/// SessionManager) already creates its own via IServiceScopeFactory internally - this
/// isn't a new pattern introduced for the desktop port.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSyteQueryCore(this IServiceCollection services, IConfiguration configuration, string sqliteConnectionString)
    {
        // --- Database Context ---
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(sqliteConnectionString));

        // --- Data Protection (encrypts stored SyteLine environment passwords) ---
        // File-system-backed key store (the framework's own default) - fine for a
        // single-machine desktop app, no database-persisted key ring needed.
        services.AddDataProtection().SetApplicationName("SyteQuery");
        services.AddScoped<IdoQueryService>();

        // Which output format version each environment's query IDO last answered with (shared, in memory).
        services.AddSingleton<IdoVersionRegistry>();

        // --- Environment Repository ---
        services.AddScoped<IUserEnvironmentRepository, UserEnvironmentRepository>();

        // --- IDO REST v2 client ---
        // RemoveAllLoggers: Infor's token endpoint takes the password in the URL path
        // (/token/{config}/{user}/{password}), and HttpClientFactory's built-in request logging
        // writes the full URL at Information level and below. Defaults hide that (log level is
        // Warning), but anyone raising the level while debugging would have written the password
        // to their logs. This client never needs that logging - IdoHttpClient logs its own
        // failures, without credentials.
        services.AddHttpClient<IIdoHttpClient, IdoHttpClient>().RemoveAllLoggers();

        // --- Multi-environment session manager (Database-backed) ---
        services.AddScoped<IEnvironmentSessionManager, DatabaseEnvironmentSessionManager>();

        // --- SQL formatter ---
        services.AddScoped<ISqlFormatter, ScriptDomSqlFormatter>();

        // --- Excel Export Service ---
        services.AddScoped<ExcelExportService>();

        // --- Data Export Service (CSV/JSON) ---
        services.AddScoped<IDataExportService, DataExportService>();

        // --- Metadata Repository ---
        services.AddScoped<IMetadataRepository, MetadataRepository>();

        // --- Metadata Cache (Shared between IntelliSense and ObjectExplorer) ---
        services.AddScoped<IMetadataCache, MetadataCache>();

        // --- Background preload of the object lists (see MetadataPreloader) ---
        services.AddScoped<IMetadataPreloader, MetadataPreloader>();

        // --- Object Definition Service ---
        services.AddScoped<IObjectDefinitionService, ObjectDefinitionService>();

        // --- Notification Service (UI notifications) ---
        services.AddScoped<INotificationService, NotificationService>();

        // --- Query History Repository and Service ---
        services.AddScoped<IQueryHistoryRepository, QueryHistoryRepository>();
        services.AddScoped<IQueryHistoryService, DatabaseQueryHistoryService>();

        // --- Query Snippet Repository and Service ---
        services.AddScoped<IQuerySnippetRepository, QuerySnippetRepository>();
        services.AddScoped<IQuerySnippetService, DatabaseQuerySnippetService>();

        // --- Query Analyzer ---
        services.AddScoped<IQueryAnalyzer, QueryAnalyzer>();

        // --- IntelliSense (SQL editor autocomplete) ---
        // Missed during the Core extraction - nothing under Features/QueryEditor's Blazor
        // component ever needed it registered explicitly there (Program.cs had it), and it
        // had no consumer at all until Phase 4c's query editor.
        services.AddScoped<IIntelliSenseProvider, IntelliSenseProvider>();

        // --- User Preferences (query display settings) ---
        services.AddScoped<IUserPreferencesService, UserPreferencesService>();

        return services;
    }

    /// <summary>
    /// Default SQLite path for the installed app: %LOCALAPPDATA%\SyteQuery\app.db.
    /// </summary>
    public static string GetDefaultSqliteConnectionString()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SyteQuery");
        Directory.CreateDirectory(appDataDir);
        return $"Data Source={Path.Combine(appDataDir, "app.db")}";
    }
}
