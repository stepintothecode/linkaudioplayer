using Android.Graphics;
using Android.Media;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using MusicPlayer.Abstractions;
using Application = Android.App.Application;

namespace MusicPlayer.App.Platforms.Android;

/// <summary>
/// Android playback built on MediaPlayer plus a media session.
/// The session is what draws the lock screen controls, and <see cref="PlaybackService"/>
/// keeps the process alive so audio continues once the screen is off.
/// </summary>
public sealed class AndroidAudioPlayer : Java.Lang.Object, IAudioPlayer
{
    private const int PositionPollMilliseconds = 500;

    private readonly MediaSessionCompat _session;
    private readonly System.Threading.Timer _positionTimer;
    private readonly HttpClient _http;
    private MediaPlayer? _player;
    private NowPlaying? _nowPlaying;
    private bool _disposed;

    /// <summary>Creates the player and its media session.</summary>
    public AndroidAudioPlayer(HttpClient http)
    {
        _http = http;

        _session = new MediaSessionCompat(Application.Context, "MusicPlayer");
        _session.SetCallback(new SessionCallback(this));
        _session.Active = true;

        _positionTimer = new System.Threading.Timer(
            _ => ReportPosition(),
            null,
            Timeout.Infinite,
            Timeout.Infinite);

        Current = this;
    }

    /// <summary>
    /// The live instance, so the Android service can reach the session without DI.
    /// Services are constructed by the platform, which cannot inject dependencies.
    /// </summary>
    public static AndroidAudioPlayer? Current { get; private set; }

    /// <inheritdoc />
    public event EventHandler<bool>? IsPlayingChanged;

    /// <inheritdoc />
    public event EventHandler<TimeSpan>? PositionChanged;

    /// <inheritdoc />
    public event EventHandler? Completed;

    /// <inheritdoc />
    public event EventHandler? NextRequested;

    /// <inheritdoc />
    public event EventHandler? PreviousRequested;

    /// <inheritdoc />
    public event EventHandler<string>? Failed;

    /// <inheritdoc />
    public bool IsPlaying => _player?.IsPlaying ?? false;

    /// <inheritdoc />
    public TimeSpan Position =>
        _player is null ? TimeSpan.Zero : TimeSpan.FromMilliseconds(_player.CurrentPosition);

    /// <inheritdoc />
    public TimeSpan Duration =>
        _player is null || _player.Duration <= 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(_player.Duration);

    /// <summary>Token the notification uses to render transport controls.</summary>
    public MediaSessionCompat.Token? SessionToken => _session.SessionToken;

    /// <summary>Details of the loaded track, for the notification.</summary>
    public NowPlaying? NowPlaying => _nowPlaying;

    /// <inheritdoc />
    public Task LoadAsync(string filePath, NowPlaying metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(metadata);

        _nowPlaying = metadata;
        ReleasePlayer();

        _player = new MediaPlayer();
        _player.SetAudioAttributes(new AudioAttributes.Builder()
            .SetContentType(AudioContentType.Music)!
            .SetUsage(AudioUsageKind.Media)!
            .Build()!);

        _player.Completion += OnCompletion;
        _player.Error += OnError;

        _player.SetDataSource(filePath);
        _player.Prepare();

        PublishMetadata(metadata);
        PlaybackService.Start();

        _player.Start();
        OnPlaybackChanged();

        _ = LoadArtworkAsync(metadata);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PlayAsync()
    {
        if (_player is not null && !_player.IsPlaying)
        {
            _player.Start();
            OnPlaybackChanged();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PauseAsync()
    {
        if (_player is not null && _player.IsPlaying)
        {
            _player.Pause();
            OnPlaybackChanged();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SeekAsync(TimeSpan position)
    {
        if (_player is not null)
        {
            _player.SeekTo((int)position.TotalMilliseconds);
            PublishPlaybackState();
            PositionChanged?.Invoke(this, position);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        ReleasePlayer();
        _nowPlaying = null;

        PublishPlaybackState();
        PlaybackService.Stop();
        IsPlayingChanged?.Invoke(this, false);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        ReleasePlayer();
        _positionTimer.Dispose();

        _session.Active = false;
        _session.Release();
        Current = null;

        return ValueTask.CompletedTask;
    }

    /// <summary>Raises the lock screen next action into the controller.</summary>
    internal void RequestNext() => NextRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises the lock screen previous action into the controller.</summary>
    internal void RequestPrevious() => PreviousRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Updates the session and notification after a transport change.</summary>
    private void OnPlaybackChanged()
    {
        PublishPlaybackState();
        PlaybackService.Refresh();

        var playing = IsPlaying;
        _positionTimer.Change(
            playing ? 0 : Timeout.Infinite,
            playing ? PositionPollMilliseconds : Timeout.Infinite);

        IsPlayingChanged?.Invoke(this, playing);
    }

    /// <summary>Pushes the current position to subscribers while playing.</summary>
    private void ReportPosition()
    {
        if (_player is not null && _player.IsPlaying)
        {
            PositionChanged?.Invoke(this, Position);
        }
    }

    // The AndroidX builders return nullable references, so these are written as
    // statements rather than chains to keep nullable analysis satisfied.

    /// <summary>Publishes track details so the lock screen can show them.</summary>
    private void PublishMetadata(NowPlaying metadata, Bitmap? artwork = null)
    {
        using var builder = new MediaMetadataCompat.Builder();

        builder.PutString(MediaMetadataCompat.MetadataKeyTitle, metadata.Title);
        builder.PutString(MediaMetadataCompat.MetadataKeyArtist, metadata.Artist ?? string.Empty);
        builder.PutLong(
            MediaMetadataCompat.MetadataKeyDuration,
            (long)(metadata.Duration ?? Duration).TotalMilliseconds);

        if (artwork is not null)
        {
            builder.PutBitmap(MediaMetadataCompat.MetadataKeyAlbumArt, artwork);
        }

        _session.SetMetadata(builder.Build());
        PublishPlaybackState();
    }

    /// <summary>Publishes transport state so the lock screen buttons and scrubber stay in sync.</summary>
    private void PublishPlaybackState()
    {
        var state = _player is null
            ? PlaybackStateCompat.StateStopped
            : IsPlaying ? PlaybackStateCompat.StatePlaying : PlaybackStateCompat.StatePaused;

        using var builder = new PlaybackStateCompat.Builder();

        builder.SetActions(
            PlaybackStateCompat.ActionPlay |
            PlaybackStateCompat.ActionPause |
            PlaybackStateCompat.ActionPlayPause |
            PlaybackStateCompat.ActionSkipToNext |
            PlaybackStateCompat.ActionSkipToPrevious |
            PlaybackStateCompat.ActionSeekTo |
            PlaybackStateCompat.ActionStop);

        builder.SetState(state, (long)Position.TotalMilliseconds, 1.0f);

        _session.SetPlaybackState(builder.Build());
    }

    /// <summary>Fetches artwork in the background. Failure just leaves the default icon.</summary>
    private async Task LoadArtworkAsync(NowPlaying metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.ArtworkUrl))
        {
            return;
        }

        try
        {
            var bytes = await _http.GetByteArrayAsync(metadata.ArtworkUrl);
            var bitmap = await BitmapFactory.DecodeByteArrayAsync(bytes, 0, bytes.Length);

            if (bitmap is not null && ReferenceEquals(_nowPlaying, metadata))
            {
                PublishMetadata(metadata, bitmap);
                PlaybackService.Refresh();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or Java.Lang.Exception)
        {
            // Artwork is decorative. Playback continues without it.
        }
    }

    private void OnCompletion(object? sender, EventArgs e)
    {
        _positionTimer.Change(Timeout.Infinite, Timeout.Infinite);
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void OnError(object? sender, MediaPlayer.ErrorEventArgs e)
    {
        Failed?.Invoke(this, $"Playback failed with code {e.What}.");
        e.Handled = true;
    }

    /// <summary>Tears down the current MediaPlayer and stops position polling.</summary>
    private void ReleasePlayer()
    {
        _positionTimer.Change(Timeout.Infinite, Timeout.Infinite);

        if (_player is null)
        {
            return;
        }

        _player.Completion -= OnCompletion;
        _player.Error -= OnError;
        _player.Stop();
        _player.Release();
        _player.Dispose();
        _player = null;
    }

    /// <summary>Routes lock screen and headset buttons back into the player.</summary>
    private sealed class SessionCallback : MediaSessionCompat.Callback
    {
        private readonly AndroidAudioPlayer _owner;

        public SessionCallback(AndroidAudioPlayer owner) => _owner = owner;

        public override void OnPlay() => _ = _owner.PlayAsync();

        public override void OnPause() => _ = _owner.PauseAsync();

        public override void OnStop() => _ = _owner.StopAsync();

        public override void OnSkipToNext() => _owner.RequestNext();

        public override void OnSkipToPrevious() => _owner.RequestPrevious();

        public override void OnSeekTo(long pos) => _ = _owner.SeekAsync(TimeSpan.FromMilliseconds(pos));
    }
}
