using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.TestSupport;

// Stands in for the Android player. Records what it was asked to load and lets a
// test raise the platform events by hand.
public sealed class FakeAudioPlayer : IAudioPlayer
{
    public event EventHandler<bool>? IsPlayingChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler? Completed;
    public event EventHandler? NextRequested;
    public event EventHandler? PreviousRequested;
    public event EventHandler<string>? Failed;

    public bool IsPlaying { get; private set; }

    public TimeSpan Position { get; private set; }

    public TimeSpan Duration { get; set; }

    /// Paths handed to LoadAsync, in order.
    public List<string> LoadedPaths { get; } = [];

    /// Metadata from the most recent load.
    public NowPlaying? LastMetadata { get; private set; }

    public int StopCount { get; private set; }

    public Task LoadAsync(string filePath, NowPlaying metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LoadedPaths.Add(filePath);
        LastMetadata = metadata;
        Position = TimeSpan.Zero;
        SetPlaying(true);

        return Task.CompletedTask;
    }

    public Task PlayAsync()
    {
        SetPlaying(true);
        return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        SetPlaying(false);
        return Task.CompletedTask;
    }

    public Task SeekAsync(TimeSpan position)
    {
        Position = position;
        PositionChanged?.Invoke(this, position);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        StopCount++;
        SetPlaying(false);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// Simulates the track reaching its end.
    public void RaiseCompleted() => Completed?.Invoke(this, EventArgs.Empty);

    /// Simulates the lock screen next button.
    public void RaiseNextRequested() => NextRequested?.Invoke(this, EventArgs.Empty);

    /// Simulates the lock screen previous button.
    public void RaisePreviousRequested() => PreviousRequested?.Invoke(this, EventArgs.Empty);

    /// Simulates a platform playback failure.
    public void RaiseFailed(string message) => Failed?.Invoke(this, message);

    /// Moves the reported position without seeking.
    public void AdvanceTo(TimeSpan position)
    {
        Position = position;
        PositionChanged?.Invoke(this, position);
    }

    private void SetPlaying(bool playing)
    {
        IsPlaying = playing;
        IsPlayingChanged?.Invoke(this, playing);
    }
}
