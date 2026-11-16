using LinkAudioPlayer.Abstractions;
using LinkAudioPlayer.Domain;
using Microsoft.Extensions.Logging;

namespace LinkAudioPlayer.Library;

/// <summary>
/// Keeps the phone's music folders in step with the files, without ever making a
/// screen wait. Pages render what is stored at once and redraw if a scan changes it.
/// </summary>
public sealed class DeviceMusicService
{
    /// <summary>How long a scan counts as fresh. Opening a page again inside it costs nothing.</summary>
    private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(30);

    private readonly IDeviceMusicSource _source;
    private readonly IDeviceFolderStore _store;
    private readonly ILogger<DeviceMusicService> _logger;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private Task<bool>? _running;
    private DateTimeOffset _lastScan = DateTimeOffset.MinValue;

    /// <summary>Creates the service over the platform's media index and the folder store.</summary>
    public DeviceMusicService(
        IDeviceMusicSource source,
        IDeviceFolderStore store,
        ILogger<DeviceMusicService> logger,
        TimeProvider? clock = null)
    {
        _source = source;
        _store = store;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Raised after a scan that changed the stored folders or songs.</summary>
    public event Action? Changed;

    /// <summary>True when the user has allowed access to music files.</summary>
    public bool HasAccess => _source.HasAccess;

    /// <summary>True while a scan is running.</summary>
    public bool IsScanning
    {
        get
        {
            lock (_gate)
            {
                return _running is { IsCompleted: false };
            }
        }
    }

    /// <summary>Asks for access, and scans straight away when given.</summary>
    public async Task<bool> RequestAccessAsync()
    {
        var granted = await _source.RequestAccessAsync();
        if (granted)
        {
            await RefreshAsync(force: true);
        }

        return granted;
    }

    /// <summary>Opens the system settings page where access can be granted.</summary>
    public void OpenSettings() => _source.OpenSettings();

    /// <summary>
    /// Scans in the background unless a scan just ran. Callers share a running scan
    /// rather than starting another. Returns true when anything changed.
    /// </summary>
    public Task<bool> RefreshAsync(bool force = false)
    {
        if (!_source.HasAccess)
        {
            return Task.FromResult(false);
        }

        lock (_gate)
        {
            if (_running is { IsCompleted: false })
            {
                return _running;
            }

            if (!force && _clock.GetUtcNow() - _lastScan < FreshFor)
            {
                return Task.FromResult(false);
            }

            _lastScan = _clock.GetUtcNow();

            // Off the caller's thread: a Blazor handler runs on the Android main thread,
            // and a cursor over a large media index must not stall the UI.
            _running = Task.Run(ScanAsync);
            return _running;
        }
    }

    /// <summary>One scan and sync. Failures are logged; a missed scan is retried next time.</summary>
    private async Task<bool> ScanAsync()
    {
        try
        {
            var songs = (await _source.ScanAsync(CancellationToken.None))
                .Where(song => !DeviceFolder.IsNotMusic(song.FolderPath))
                .ToList();

            var changed = await _store.SyncAsync(songs, CancellationToken.None);

            _logger.LogInformation("Scanned {Count} phone song(s), changed: {Changed}", songs.Count, changed);

            if (changed)
            {
                Changed?.Invoke();
            }

            return changed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scanning the phone's music failed");

            lock (_gate)
            {
                _lastScan = DateTimeOffset.MinValue;
            }

            return false;
        }
    }
}
