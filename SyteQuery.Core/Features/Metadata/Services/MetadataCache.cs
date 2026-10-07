using System.Collections.Concurrent;
using SyteQuery.Features.Metadata.Models;

namespace SyteQuery.Features.Metadata.Services;

/// <summary>
/// Thread-safe shared cache for database metadata with lazy loading
/// </summary>
public sealed class MetadataCache : IMetadataCache
{
    private readonly IMetadataRepository _repository;
    private readonly ConcurrentDictionary<string, EnvironmentCache> _caches = new();

    public MetadataCache(IMetadataRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<DatabaseObject>> GetTablesAsync(string envId)
    {
        var cache = GetOrCreateCache(envId);
        if (cache.Tables != null)
            return cache.Tables;

        var tables = await _repository.GetTablesAsync(envId);
        cache.Tables = tables;
        return tables;
    }

    public async Task<List<DatabaseObject>> GetViewsAsync(string envId)
    {
        var cache = GetOrCreateCache(envId);
        if (cache.Views != null)
            return cache.Views;

        var views = await _repository.GetViewsAsync(envId);
        cache.Views = views;
        return views;
    }

    public async Task<List<DatabaseObject>> GetStoredProceduresAsync(string envId)
    {
        var cache = GetOrCreateCache(envId);
        if (cache.StoredProcedures != null)
            return cache.StoredProcedures;

        var procs = await _repository.GetStoredProceduresAsync(envId);
        cache.StoredProcedures = procs;
        return procs;
    }

    public async Task<List<DatabaseObject>> GetScalarFunctionsAsync(string envId)
    {
        var cache = GetOrCreateCache(envId);
        if (cache.ScalarFunctions != null)
            return cache.ScalarFunctions;

        var funcs = await _repository.GetScalarFunctionsAsync(envId);
        cache.ScalarFunctions = funcs;
        return funcs;
    }

    public async Task<List<DatabaseObject>> GetTableValuedFunctionsAsync(string envId)
    {
        var cache = GetOrCreateCache(envId);
        if (cache.TableValuedFunctions != null)
            return cache.TableValuedFunctions;

        var funcs = await _repository.GetTableValuedFunctionsAsync(envId);
        cache.TableValuedFunctions = funcs;
        return funcs;
    }

    public async Task<List<DatabaseObject>> GetAggregateFunctionsAsync(string envId)
    {
        var cache = GetOrCreateCache(envId);
        if (cache.AggregateFunctions != null)
            return cache.AggregateFunctions;

        var funcs = await _repository.GetAggregateFunctionsAsync(envId);
        cache.AggregateFunctions = funcs;
        return funcs;
    }

    public async Task<List<ColumnInfo>> GetTableColumnsAsync(string envId, string schemaName, string tableName)
    {
        var cache = GetOrCreateCache(envId);
        var key = $"{schemaName}.{tableName}";

        if (cache.TableColumns.TryGetValue(key, out var columns))
            return columns;

        var fetchedColumns = await _repository.GetTableColumnsAsync(envId, schemaName, tableName);
        cache.TableColumns[key] = fetchedColumns;
        return fetchedColumns;
    }

    public async Task<List<ColumnInfo>> GetViewColumnsAsync(string envId, string schemaName, string viewName)
    {
        var cache = GetOrCreateCache(envId);
        var key = $"{schemaName}.{viewName}";

        if (cache.ViewColumns.TryGetValue(key, out var columns))
            return columns;

        var fetchedColumns = await _repository.GetViewColumnsAsync(envId, schemaName, viewName);
        cache.ViewColumns[key] = fetchedColumns;
        return fetchedColumns;
    }

    public async Task<List<TriggerInfo>> GetTableTriggersAsync(string envId, string schemaName, string tableName)
    {
        var cache = GetOrCreateCache(envId);
        var key = $"{schemaName}.{tableName}";

        if (cache.TableTriggers.TryGetValue(key, out var triggers))
            return triggers;

        var fetched = await _repository.GetTableTriggersAsync(envId, schemaName, tableName);
        cache.TableTriggers[key] = fetched;
        return fetched;
    }

    public void InvalidateEnvironment(string envId)
    {
        _caches.TryRemove(envId, out _);
    }

    public void InvalidateAll()
    {
        _caches.Clear();
    }

    private EnvironmentCache GetOrCreateCache(string envId)
    {
        return _caches.GetOrAdd(envId, _ => new EnvironmentCache());
    }

    /// <summary>
    /// Per-environment cache storage
    /// </summary>
    private sealed class EnvironmentCache
    {
        public List<DatabaseObject>? Tables { get; set; }
        public List<DatabaseObject>? Views { get; set; }
        public List<DatabaseObject>? StoredProcedures { get; set; }
        public List<DatabaseObject>? ScalarFunctions { get; set; }
        public List<DatabaseObject>? TableValuedFunctions { get; set; }
        public List<DatabaseObject>? AggregateFunctions { get; set; }
        public ConcurrentDictionary<string, List<ColumnInfo>> TableColumns { get; } = new();
        public ConcurrentDictionary<string, List<ColumnInfo>> ViewColumns { get; } = new();
        public ConcurrentDictionary<string, List<TriggerInfo>> TableTriggers { get; } = new();
    }
}
