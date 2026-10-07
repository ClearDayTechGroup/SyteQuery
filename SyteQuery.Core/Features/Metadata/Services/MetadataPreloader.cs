namespace SyteQuery.Features.Metadata.Services;

/// <summary>Progress of a preload: a human-readable step and, when known, how far along (0..1).</summary>
public sealed record PreloadProgress(string Message, double? Fraction);

/// <summary>
/// Loads an environment's object lists - tables, views, stored procedures and the three kinds of
/// function - into <see cref="IMetadataCache"/> ahead of time, so the first expand of each Object
/// Explorer folder (and IntelliSense) doesn't wait on the server. Deliberately stops there: a column
/// preload was tried and dropped, because a real SyteLine environment has well over 100,000 columns
/// and loading them all took far longer than it saved. Columns and triggers load per table/view when
/// you expand it, and are cached after that first load.
/// </summary>
public interface IMetadataPreloader
{
    Task PreloadAsync(string envId, IProgress<PreloadProgress>? progress, CancellationToken ct);
}

public sealed class MetadataPreloader : IMetadataPreloader
{
    private readonly IMetadataCache _cache;

    public MetadataPreloader(IMetadataCache cache)
    {
        _cache = cache;
    }

    public async Task PreloadAsync(string envId, IProgress<PreloadProgress>? progress, CancellationToken ct)
    {
        var steps = new (string Name, Func<Task> Load)[]
        {
            ("tables", () => _cache.GetTablesAsync(envId)),
            ("views", () => _cache.GetViewsAsync(envId)),
            ("stored procedures", () => _cache.GetStoredProceduresAsync(envId)),
            ("scalar functions", () => _cache.GetScalarFunctionsAsync(envId)),
            ("table-valued functions", () => _cache.GetTableValuedFunctionsAsync(envId)),
            ("aggregate functions", () => _cache.GetAggregateFunctionsAsync(envId)),
        };

        for (var i = 0; i < steps.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new PreloadProgress($"Loading {steps[i].Name}...", (double)i / steps.Length));
            await steps[i].Load();
        }

        ct.ThrowIfCancellationRequested();
        progress?.Report(new PreloadProgress("Done", 1));
    }
}
