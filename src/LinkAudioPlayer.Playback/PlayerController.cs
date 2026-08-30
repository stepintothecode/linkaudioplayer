using LinkAudioPlayer.Abstractions;
using LinkAudioPlayer.Domain;
using Microsoft.Extensions.Logging;

namespace LinkAudioPlayer.Playback;

/// <summary>
/// Owns the playback queue and drives the platform audio player.
/// Platform agnostic, so the queue rules are unit testable without a device.
/// </summary>
public sealed class PlayerController : IAsyncDisposable
{
    private readonly IAudioPlayer _player;
    private readonly IMediaCache _cache;
    private readonly ILogger<PlayerController> _logger;
    private readonly List<Track> _queue = [];
    private CancellationTokenSource? _loadCancellation;

    /// <summary>Creates the controller over the platform player and the media cache.</summary>
    public PlayerController(IAudioPlayer player, IMediaCache cache, ILogger<PlayerController> logger)
    {
        _player = player;
        _cache = cache;
        _logger = logger;

        _player.IsPlayingChanged += OnIsPlayingChanged;
        _player.PositionChanged += OnPositionChanged;
        _player.Completed += OnCompleted;
        _player.NextRequested += OnNextRequested;
        _player.PreviousRequested += OnPreviousRequested;
        _player.Failed += OnFailed;
    }

    /// <summary>Raised whenever queue, transport or progress state changes.</summary>
    public event Action? StateChanged;

    /// <summary>Tracks queued for playback, in order.</summary>
    public IReadOnlyList<Track> Queue => _queue;

    /// <summary>Index of the loaded track, or -1 when the queue is empty.</summary>
    public int CurrentIndex { get; private set; } = -1;

    /// <summary>Track currently loaded, or null.</summary>
    public Track? Current => CurrentIndex >= 0 && CurrentIndex < _queue.Count ? _queue[CurrentIndex] : null;

    /// <summary>True while audio is playing.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>True while a track is downloading before playback can start.</summary>
    public bool IsPreparing { get; private set; }

    /// <summary>Download progress from 0 to 1 while preparing.</summary>
    public double PrepareProgress { get; private set; }

    /// <summary>Current playback position.</summary>
    public TimeSpan Position { get; private set; }

    /// <summary>Length of the loaded track, falling back to the stored duration.</summary>
    public TimeSpan Duration =>
        _player.Duration > TimeSpan.Zero
            ? _player.Duration
            : TimeSpan.FromSeconds(Current?.DurationSeconds ?? 0);

    /// <summary>What happens when a track finishes.</summary>
    public RepeatMode Repeat { get; private set; } = RepeatMode.Off;

    /// <summary>Last playback or download error, cleared when a new track loads.</summary>
    public string? Error { get; private set; }

    /// <summary>Replaces the queue and starts playing at the given index.</summary>
    public async Task PlayQueueAsync(IEnumerable<Track> tracks, int startIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(tracks);

        _queue.Clear();
        _queue.AddRange(tracks);
        CurrentIndex = _queue.Count == 0 ? -1 : Math.Clamp(startIndex, 0, _queue.Count - 1);

        await LoadCurrentAsync();
    }

    /// <summary>Cycles the repeat mode between off, all and one.</summary>
    public void CycleRepeat()
    {
        Repeat = Repeat switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            _ => RepeatMode.Off,
        };

        StateChanged?.Invoke();
    }

    /// <summary>Toggles between playing and paused.</summary>
    public async Task TogglePlayPauseAsync()
    {
        if (Current is null)
        {
            return;
        }

        if (IsPlaying)
        {
            await _player.PauseAsync();
        }
        else
        {
            await _player.PlayAsync();
        }
    }

    /// <summary>Advances to the next track, honouring the repeat mode.</summary>
    public async Task NextAsync()
    {
        var next = NextIndex(manual: true);
        if (next is null)
        {
            return;
        }

        CurrentIndex = next.Value;
        await LoadCurrentAsync();
    }

    /// <summary>
    /// Restarts the current track, or steps back when already near its start.
    /// Matches the lock screen previous button on other players.
    /// </summary>
    public async Task PreviousAsync()
    {
        if (Current is null)
        {
            return;
        }

        if (Position > TimeSpan.FromSeconds(3) || CurrentIndex == 0)
        {
            await SeekAsync(TimeSpan.Zero);
            return;
        }

        CurrentIndex--;
        await LoadCurrentAsync();
    }

    /// <summary>Seeks the current track.</summary>
    public async Task SeekAsync(TimeSpan position)
    {
        if (Current is not null)
        {
            await _player.SeekAsync(position);
        }
    }

    /// <summary>Stops playback and clears the queue.</summary>
    public async Task StopAsync()
    {
        await CancelLoadAsync();
        await _player.StopAsync();

        _queue.Clear();
        CurrentIndex = -1;
        IsPlaying = false;
        IsPreparing = false;
        Position = TimeSpan.Zero;
        StateChanged?.Invoke();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _player.IsPlayingChanged -= OnIsPlayingChanged;
        _player.PositionChanged -= OnPositionChanged;
        _player.Completed -= OnCompleted;
        _player.NextRequested -= OnNextRequested;
        _player.PreviousRequested -= OnPreviousRequested;
        _player.Failed -= OnFailed;

        await CancelLoadAsync();
        await _player.DisposeAsync();
    }

    /// <summary>
    /// Chooses the index to move to.
    /// A manual skip always advances; an automatic advance obeys the repeat mode.
    /// </summary>
    private int? NextIndex(bool manual)
    {
        if (CurrentIndex < 0 || _queue.Count == 0)
        {
            return null;
        }

        if (!manual && Repeat == RepeatMode.One)
        {
            return CurrentIndex;
        }

        if (CurrentIndex + 1 < _queue.Count)
        {
            return CurrentIndex + 1;
        }

        // At the end of the queue. Wrap only when repeating everything.
        return Repeat == RepeatMode.All ? 0 : null;
    }

    /// <summary>Downloads the current track if needed, then hands it to the platform player.</summary>
    private async Task LoadCurrentAsync()
    {
        var track = Current;
        if (track is null)
        {
            return;
        }

        await CancelLoadAsync();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;

        Error = null;
        Position = TimeSpan.Zero;
        PrepareProgress = 0;
        StateChanged?.Invoke();

        try
        {
            var path = _cache.TryGetPath(track.CacheKey);
            _logger.LogInformation(
                "Loading '{Title}', cached: {Cached}", track.Title, path is not null);

            if (path is null)
            {
                IsPreparing = true;
                StateChanged?.Invoke();

                var progress = new Progress<double>(value =>
                {
                    PrepareProgress = value;
                    StateChanged?.Invoke();
                });

                path = await _cache.StoreAsync(
                    track.CacheKey,
                    new Uri(track.SourceUrl),
                    progress,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var metadata = new NowPlaying(
                track.Title,
                track.Artist,
                track.DurationSeconds is > 0 ? TimeSpan.FromSeconds(track.DurationSeconds.Value) : null,
                track.ThumbnailUrl);

            _logger.LogInformation("Handing '{Path}' to the platform player", path);
            await _player.LoadAsync(path, metadata, cancellationToken);
            _logger.LogInformation("Playback started for '{Title}'", track.Title);
        }
        catch (OperationCanceledException)
        {
            // Superseded by another load, or stopped. Logged rather than ignored,
            // because a silent cancellation looks identical to nothing happening.
            _logger.LogInformation("Loading '{Title}' was cancelled", track.Title);
        }
        catch (Exception ex)
        {
            // Must be logged here. This is the only place a playback failure surfaces,
            // and an unlogged one leaves the user with a dead play button and no clue.
            _logger.LogError(ex, "Could not play '{Title}' from {Url}", track.Title, track.SourceUrl);
            Error = Describe(ex);
        }
        finally
        {
            IsPreparing = false;
            StateChanged?.Invoke();
        }
    }

    /// <summary>Innermost message, which is usually the useful one.</summary>
    private static string Describe(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }

    /// <summary>Cancels any load still in flight.</summary>
    private async Task CancelLoadAsync()
    {
        if (_loadCancellation is null)
        {
            return;
        }

        await _loadCancellation.CancelAsync();
        _loadCancellation.Dispose();
        _loadCancellation = null;
    }

    private void OnIsPlayingChanged(object? sender, bool isPlaying)
    {
        IsPlaying = isPlaying;
        StateChanged?.Invoke();
    }

    private void OnPositionChanged(object? sender, TimeSpan position)
    {
        Position = position;
        StateChanged?.Invoke();
    }

    private async void OnCompleted(object? sender, EventArgs e)
    {
        var next = NextIndex(manual: false);

        if (next is null)
        {
            IsPlaying = false;
            StateChanged?.Invoke();
            return;
        }

        CurrentIndex = next.Value;
        await LoadCurrentAsync();
    }

    private async void OnNextRequested(object? sender, EventArgs e) => await NextAsync();

    private async void OnPreviousRequested(object? sender, EventArgs e) => await PreviousAsync();

    private void OnFailed(object? sender, string message)
    {
        _logger.LogError("Platform player reported: {Message}", message);
        Error = message;
        IsPlaying = false;
        StateChanged?.Invoke();
    }
}
