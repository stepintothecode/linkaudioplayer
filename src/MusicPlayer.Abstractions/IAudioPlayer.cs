namespace MusicPlayer.Abstractions;

/// <summary>
/// Platform audio output. The Android implementation owns a foreground service and a
/// media session, which is what keeps playback alive with the screen locked.
/// </summary>
public interface IAudioPlayer : IAsyncDisposable
{
    /// <summary>Raised when playback starts, pauses or stops.</summary>
    event EventHandler<bool>? IsPlayingChanged;

    /// <summary>Raised as playback progresses.</summary>
    event EventHandler<TimeSpan>? PositionChanged;

    /// <summary>Raised when the loaded track reaches its end.</summary>
    event EventHandler? Completed;

    /// <summary>Raised by the lock screen or headset next button.</summary>
    event EventHandler? NextRequested;

    /// <summary>Raised by the lock screen or headset previous button.</summary>
    event EventHandler? PreviousRequested;

    /// <summary>Raised when the platform reports a playback failure.</summary>
    event EventHandler<string>? Failed;

    /// <summary>True while audio is playing.</summary>
    bool IsPlaying { get; }

    /// <summary>Current playback position.</summary>
    TimeSpan Position { get; }

    /// <summary>Length of the loaded track, or zero when unknown.</summary>
    TimeSpan Duration { get; }

    /// <summary>Loads a local audio file and begins playing it.</summary>
    /// <param name="filePath">Absolute path to the downloaded audio.</param>
    /// <param name="metadata">Details shown on the lock screen.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    Task LoadAsync(string filePath, NowPlaying metadata, CancellationToken cancellationToken);

    /// <summary>Resumes playback.</summary>
    Task PlayAsync();

    /// <summary>Pauses without unloading the current track.</summary>
    Task PauseAsync();

    /// <summary>Seeks to a position within the loaded track.</summary>
    Task SeekAsync(TimeSpan position);

    /// <summary>Stops playback and releases the current track.</summary>
    Task StopAsync();
}
