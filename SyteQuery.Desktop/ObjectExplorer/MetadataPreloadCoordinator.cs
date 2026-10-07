using System.ComponentModel;
using System.Runtime.CompilerServices;
using SyteQuery.Features.Environments.Services;
using SyteQuery.Features.IntelliSense.Services;
using SyteQuery.Features.Metadata.Services;

namespace SyteQuery.Desktop.ObjectExplorer;

/// <summary>
/// Runs <see cref="IMetadataPreloader"/> in the background for each environment - at startup, after
/// an environment is added or edited, and when the user hits Refresh - and exposes a status line and
/// progress fraction for the window's status bar. One environment is loaded at a time (they share the
/// same UI thread and, usually, the same tenant), and a refresh cancels any in-flight load for that
/// environment before invalidating its cache so a stale run can't repopulate it.
/// All members are meant to be called from the UI thread.
/// </summary>
public sealed class MetadataPreloadCoordinator : INotifyPropertyChanged
{
    private readonly IMetadataPreloader _preloader;
    private readonly IMetadataCache _cache;
    private readonly IIntelliSenseProvider _intelliSense;
    private readonly IEnvironmentSessionManager _envMgr;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, CancellationTokenSource> _running = new();
    private readonly HashSet<string> _loaded = new();

    private string _statusText = "Ready";
    private bool _isBusy;
    private double _fraction;

    public MetadataPreloadCoordinator(
        IMetadataPreloader preloader,
        IMetadataCache cache,
        IIntelliSenseProvider intelliSense,
        IEnvironmentSessionManager envMgr)
    {
        _preloader = preloader;
        _cache = cache;
        _intelliSense = intelliSense;
        _envMgr = envMgr;
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    /// <summary>0..1 progress of the load currently running.</summary>
    public double Fraction
    {
        get => _fraction;
        private set => SetField(ref _fraction, value);
    }

    /// <summary>Preload every configured environment (startup).</summary>
    public void StartPreloadAll()
    {
        foreach (var profile in _envMgr.Profiles)
            StartPreload(profile.Id);
    }

    /// <summary>Preload any environment that hasn't been loaded this session (after the
    /// Environments dialog closes - picks up newly added ones).</summary>
    public void StartPreloadForNew()
    {
        foreach (var profile in _envMgr.Profiles)
        {
            if (!_loaded.Contains(profile.Id))
                StartPreload(profile.Id);
        }
    }

    public void StartPreload(string envId)
    {
        if (_running.ContainsKey(envId))
            return;

        var cts = new CancellationTokenSource();
        _running[envId] = cts;
        _ = RunAsync(envId, cts);
    }

    /// <summary>Throw away everything cached for the environment (object lists, columns, triggers,
    /// IntelliSense) and load it again from the server.</summary>
    public void Refresh(string envId)
    {
        Cancel(envId);
        _cache.InvalidateEnvironment(envId);
        _intelliSense.ClearCache(envId);
        _loaded.Remove(envId);
        StartPreload(envId);
    }

    /// <summary>Stop loading an environment and forget it (it was removed).</summary>
    public void Forget(string envId)
    {
        Cancel(envId);
        _loaded.Remove(envId);
    }

    private void Cancel(string envId)
    {
        if (_running.Remove(envId, out var cts))
            cts.Cancel();
    }

    private async Task RunAsync(string envId, CancellationTokenSource cts)
    {
        var name = _envMgr.Profiles.FirstOrDefault(p => p.Id == envId)?.Name ?? envId;
        var failed = false;

        try
        {
            IsBusy = true;
            StatusText = $"{name}: waiting to load metadata...";
            await _gate.WaitAsync(cts.Token);
            try
            {
                // Progress<T> captures the UI synchronization context, so status updates arrive on
                // the UI thread even though the work below runs on the thread pool.
                var progress = new Progress<PreloadProgress>(p =>
                {
                    StatusText = $"{name}: {p.Message}";
                    if (p.Fraction is { } f)
                        Fraction = f;
                });

                // Off the UI thread: parsing and grouping thousands of column rows per page would
                // otherwise stutter the window.
                await Task.Run(() => _preloader.PreloadAsync(envId, progress, cts.Token), cts.Token);
                _loaded.Add(envId);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled by a refresh or removal - the replacement run (if any) reports its own status.
        }
        catch (Exception ex)
        {
            failed = true;
            StatusText = $"Couldn't load metadata for {name}: {ex.Message}";
        }
        finally
        {
            // Only clear our own entry - Refresh may already have replaced it with a newer run.
            if (_running.TryGetValue(envId, out var current) && ReferenceEquals(current, cts))
                _running.Remove(envId);

            cts.Dispose();

            if (_running.Count == 0)
            {
                IsBusy = false;
                Fraction = 0;
                if (!failed)
                    StatusText = "Ready";
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
