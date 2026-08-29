using Microsoft.JSInterop;
using MusicPlayer.Contracts;

namespace MusicPlayer.Web.Services;

/// <summary>
/// Holds the playback queue and drives the JavaScript audio element.
/// Registered as a singleton so playback survives navigation between pages.
/// </summary>
public sealed class PlayerService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly List<TrackDto> _queue = [];
    private DotNetObjectReference<PlayerService>? _selfReference;
    private IJSObjectReference? _module;

    /// <summary>Creates the service over the Blazor JavaScript runtime.</summary>
    public PlayerService(IJSRuntime js) => _js = js;

    /// <summary>Raised whenever playback state, position or queue changes.</summary>
    public event Action? StateChanged;

    /// <summary>Tracks queued for playback, in order.</summary>
    public IReadOnlyList<TrackDto> Queue => _queue;

    /// <summary>Index of the playing track, or -1 when the queue is empty.</summary>
    public int CurrentIndex { get; private set; } = -1;

    /// <summary>Track currently loaded, or null.</summary>
    public TrackDto? Current => CurrentIndex >= 0 && CurrentIndex < _queue.Count ? _queue[CurrentIndex] : null;

    /// <summary>True while audio is playing.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Playback position in seconds.</summary>
    public double PositionSeconds { get; private set; }

    /// <summary>Length of the loaded track in seconds, or 0 when unknown.</summary>
    public double DurationSeconds { get; private set; }

    /// <summary>Last playback error, cleared when a new track loads.</summary>
    public string? Error { get; private set; }

    /// <summary>Replaces the queue and starts playing at the given index.</summary>
    /// <param name="tracks">Tracks to queue.</param>
    /// <param name="startIndex">Index to begin at.</param>
    public async Task PlayQueueAsync(IEnumerable<TrackDto> tracks, int startIndex = 0)
    {
        _queue.Clear();
        _queue.AddRange(tracks);
        CurrentIndex = _queue.Count == 0 ? -1 : Math.Clamp(startIndex, 0, _queue.Count - 1);
        await LoadCurrentAsync();
    }

    /// <summary>Toggles between playing and paused.</summary>
    public async Task TogglePlayPauseAsync()
    {
        var module = await GetModuleAsync();

        if (IsPlaying)
        {
            await module.InvokeVoidAsync("pause");
        }
        else
        {
            await module.InvokeVoidAsync("play");
        }
    }

    /// <summary>Advances to the next track. Stops at the end of the queue.</summary>
    public async Task NextAsync()
    {
        if (CurrentIndex < 0 || CurrentIndex + 1 >= _queue.Count)
        {
            return;
        }

        CurrentIndex++;
        await LoadCurrentAsync();
    }

    /// <summary>
    /// Restarts the current track, or moves to the previous one when already near the start.
    /// This matches the behaviour of the lock screen previous button on other players.
    /// </summary>
    public async Task PreviousAsync()
    {
        if (CurrentIndex < 0)
        {
            return;
        }

        if (PositionSeconds > 3 || CurrentIndex == 0)
        {
            await SeekAsync(0);
            return;
        }

        CurrentIndex--;
        await LoadCurrentAsync();
    }

    /// <summary>Seeks the current track to a position in seconds.</summary>
    public async Task SeekAsync(double seconds)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("seek", seconds);
    }

    /// <summary>Stops playback and clears the queue.</summary>
    public async Task StopAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("stop");

        _queue.Clear();
        CurrentIndex = -1;
        IsPlaying = false;
        PositionSeconds = 0;
        DurationSeconds = 0;
        StateChanged?.Invoke();
    }

    /// <summary>Called from JavaScript when the audio element starts or stops.</summary>
    [JSInvokable]
    public void OnPlaybackStateChanged(bool isPlaying)
    {
        IsPlaying = isPlaying;
        StateChanged?.Invoke();
    }

    /// <summary>Called from JavaScript as playback progresses.</summary>
    [JSInvokable]
    public void OnPositionChanged(double position, double duration)
    {
        PositionSeconds = position;
        DurationSeconds = duration;
        StateChanged?.Invoke();
    }

    /// <summary>Called from JavaScript when a track finishes. Advances the queue.</summary>
    [JSInvokable]
    public async Task OnTrackEnded() => await NextAsync();

    /// <summary>Called from the lock screen next button.</summary>
    [JSInvokable]
    public async Task OnNextRequested() => await NextAsync();

    /// <summary>Called from the lock screen previous button.</summary>
    [JSInvokable]
    public async Task OnPreviousRequested() => await PreviousAsync();

    /// <summary>Called from JavaScript when the audio element reports a failure.</summary>
    [JSInvokable]
    public void OnPlaybackError(string message)
    {
        Error = message;
        IsPlaying = false;
        StateChanged?.Invoke();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The browser is already gone.
            }
        }

        _selfReference?.Dispose();
    }

    /// <summary>Sends the current track to the audio element.</summary>
    private async Task LoadCurrentAsync()
    {
        var track = Current;
        if (track is null)
        {
            return;
        }

        Error = null;
        PositionSeconds = 0;
        DurationSeconds = track.DurationSeconds ?? 0;
        StateChanged?.Invoke();

        var module = await GetModuleAsync();
        await module.InvokeVoidAsync(
            "load",
            ApiRoutes.Stream(track.Id),
            new { title = track.Title, artist = track.Artist, thumbnailUrl = track.ThumbnailUrl });
    }

    /// <summary>Imports the JavaScript module on first use and registers the callback target.</summary>
    private async Task<IJSObjectReference> GetModuleAsync()
    {
        if (_module is not null)
        {
            return _module;
        }

        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/audio-player.js");
        _selfReference = DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("initialize", _selfReference);

        return _module;
    }
}
