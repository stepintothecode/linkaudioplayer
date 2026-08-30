using Android.Graphics;
using Android.Media;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using LinkAudioPlayer.Abstractions;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;

namespace LinkAudioPlayer.App.Platforms.Android;

/// <summary>
/// Android playback built on MediaPlayer plus a media session.
/// The session is what draws the lock screen controls, and <see cref="PlaybackService"/>
/// keeps the process alive so audio continues once the screen is off.
/// </summary>
/// <remarks>
/// Every MediaPlayer and MediaSession call runs on the Android main thread. These types
/// need a Looper, and creating one on a thread pool thread throws Java.Lang.RuntimeException.
/// Playback is driven from continuations after a download, so the marshalling is not optional.
/// </remarks>
public sealed class AndroidAudioPlayer : Java.Lang.Object, IAudioPlayer
{
    private const int PositionPollMilliseconds = 250;

    private readonly ILogger<AndroidAudioPlayer> _logger;
    private readonly HttpClient _http;
    private readonly System.Threading.Timer _positionTimer;
    private MediaSessionCompat? _session;
    private MediaPlayer? _player;
    private NowPlaying? _nowPlaying;
    private bool _disposed;

    /// <summary>Creates the player. The media session is built lazily on the main thread.</summary>
    public AndroidAudioPlayer(HttpClient http, ILogger<AndroidAudioPlayer> logger)
    {
        _http = http;
        _logger = logger;

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
    public bool IsPlaying { get; private set; }

    /// <inheritdoc />
    public TimeSpan Position { get; private set; }

    /// <inheritdoc />
    public TimeSpan Duration { get; private set; }

    /// <summary>Token the notification uses to render transport controls.</summary>
    public MediaSessionCompat.Token? SessionToken => _session?.SessionToken;

    /// <summary>Details of the loaded track, for the notification.</summary>
    public NowPlaying? NowPlaying => _nowPlaying;

    /// <inheritdoc />
    public async Task LoadAsync(string filePath, NowPlaying metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(metadata);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The downloaded audio file is missing.", filePath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _nowPlaying = metadata;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            EnsureSession();
            ReleasePlayer();

            var player = new MediaPlayer();
            player.SetAudioAttributes(new AudioAttributes.Builder()
                .SetContentType(AudioContentType.Music)!
                .SetUsage(AudioUsageKind.Media)!
                .Build()!);

            player.Completion += OnCompletion;
            player.Error += OnError;
            _player = player;

            player.SetDataSource(filePath);
            await PrepareAsync(player, cancellationToken);

            Duration = player.Duration > 0 ? TimeSpan.FromMilliseconds(player.Duration) : TimeSpan.Zero;
            Position = TimeSpan.Zero;

            PublishMetadata(metadata);
            StartService();

            player.Start();
            OnPlaybackChanged();
        });

        // Also off the main thread. It is reached from a continuation that resumes there.
        _ = Task.Run(() => LoadArtworkAsync(metadata));
    }

    /// <inheritdoc />
    public Task PlayAsync() => MainThread.InvokeOnMainThreadAsync(() =>
    {
        if (_player is not null && !_player.IsPlaying)
        {
            _player.Start();
            StartService();
            OnPlaybackChanged();
        }
    });

    /// <inheritdoc />
    public Task PauseAsync() => MainThread.InvokeOnMainThreadAsync(() =>
    {
        if (_player is not null && _player.IsPlaying)
        {
            _player.Pause();
            OnPlaybackChanged();
        }
    });

    /// <inheritdoc />
    public Task SeekAsync(TimeSpan position) => MainThread.InvokeOnMainThreadAsync(() =>
    {
        if (_player is null)
        {
            return;
        }

        _player.SeekTo((int)position.TotalMilliseconds);
        Position = position;
        PublishPlaybackState();
        PositionChanged?.Invoke(this, position);
    });

    /// <inheritdoc />
    public Task StopAsync() => MainThread.InvokeOnMainThreadAsync(() =>
    {
        ReleasePlayer();
        _nowPlaying = null;
        Position = TimeSpan.Zero;
        Duration = TimeSpan.Zero;
        IsPlaying = false;

        PublishPlaybackState();
        PlaybackService.Stop();
        IsPlayingChanged?.Invoke(this, false);
    });

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _positionTimer.DisposeAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ReleasePlayer();

            if (_session is not null)
            {
                _session.Active = false;
                _session.Release();
                _session = null;
            }
        });

        Current = null;
    }

    /// <summary>Raises the lock screen next action into the controller.</summary>
    internal void RequestNext() => NextRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises the lock screen previous action into the controller.</summary>
    internal void RequestPrevious() => PreviousRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Creates the media session on first use. Must run on the main thread.</summary>
    private void EnsureSession()
    {
        if (_session is not null)
        {
            return;
        }

        _session = new MediaSessionCompat(Application.Context, "LinkAudioPlayer");
        _session.SetCallback(new SessionCallback(this));
        _session.Active = true;
    }

    /// <summary>Prepares the player without blocking the main thread.</summary>
    private static Task PrepareAsync(MediaPlayer player, CancellationToken cancellationToken)
    {
        var prepared = new TaskCompletionSource();

        void OnPrepared(object? sender, EventArgs e) => prepared.TrySetResult();
        void OnPrepareError(object? sender, MediaPlayer.ErrorEventArgs e)
        {
            e.Handled = true;
            prepared.TrySetException(new InvalidOperationException(
                $"Android could not decode this audio (error {e.What}, extra {e.Extra})."));
        }

        player.Prepared += OnPrepared;
        player.Error += OnPrepareError;

        using var registration = cancellationToken.Register(() => prepared.TrySetCanceled(cancellationToken));

        try
        {
            player.PrepareAsync();
            return prepared.Task.ContinueWith(
                task =>
                {
                    player.Prepared -= OnPrepared;
                    player.Error -= OnPrepareError;
                    return task;
                },
                TaskScheduler.Default).Unwrap();
        }
        catch (Exception)
        {
            player.Prepared -= OnPrepared;
            player.Error -= OnPrepareError;
            throw;
        }
    }

    /// <summary>
    /// Starts the foreground service, tolerating a refusal.
    /// Android blocks foreground service starts in some states, and audio still plays
    /// while the app is in front, so this must not take playback down with it.
    /// </summary>
    private void StartService()
    {
        try
        {
            PlaybackService.Start();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not start the playback foreground service");
        }
    }

    /// <summary>Updates the session and notification after a transport change.</summary>
    private void OnPlaybackChanged()
    {
        IsPlaying = _player?.IsPlaying ?? false;

        PublishPlaybackState();
        PlaybackService.Refresh();

        _positionTimer.Change(
            IsPlaying ? 0 : Timeout.Infinite,
            IsPlaying ? PositionPollMilliseconds : Timeout.Infinite);

        IsPlayingChanged?.Invoke(this, IsPlaying);
    }

    /// <summary>Pushes the current position to subscribers while playing.</summary>
    private void ReportPosition() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (_player is null || !_player.IsPlaying)
        {
            return;
        }

        Position = TimeSpan.FromMilliseconds(_player.CurrentPosition);
        PositionChanged?.Invoke(this, Position);
    });

    // The AndroidX builders return nullable references, so these are written as
    // statements rather than chains to keep nullable analysis satisfied.

    /// <summary>Publishes track details so the lock screen can show them.</summary>
    private void PublishMetadata(NowPlaying metadata, Bitmap? artwork = null)
    {
        if (_session is null)
        {
            return;
        }

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
        if (_session is null)
        {
            return;
        }

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

            if (bitmap is null || !ReferenceEquals(_nowPlaying, metadata))
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                PublishMetadata(metadata, bitmap);
                PlaybackService.Refresh();
            });
        }
        catch (Exception ex)
        {
            // Artwork is decorative. Playback continues without it.
            _logger.LogDebug(ex, "Could not load artwork from {Url}", metadata.ArtworkUrl);
        }
    }

    private void OnCompletion(object? sender, EventArgs e)
    {
        _positionTimer.Change(Timeout.Infinite, Timeout.Infinite);
        IsPlaying = false;
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void OnError(object? sender, MediaPlayer.ErrorEventArgs e)
    {
        e.Handled = true;
        _logger.LogError("MediaPlayer error {What}, extra {Extra}", e.What, e.Extra);
        Failed?.Invoke(this, $"Playback failed (error {e.What}).");
    }

    /// <summary>Tears down the current MediaPlayer. Must run on the main thread.</summary>
    private void ReleasePlayer()
    {
        _positionTimer.Change(Timeout.Infinite, Timeout.Infinite);

        if (_player is null)
        {
            return;
        }

        var player = _player;
        _player = null;

        player.Completion -= OnCompletion;
        player.Error -= OnError;

        try
        {
            player.Stop();
        }
        catch (Exception ex)
        {
            // Stop throws when the player was never prepared. Releasing still has to happen.
            _logger.LogDebug(ex, "MediaPlayer.Stop failed during release");
        }

        player.Release();
        player.Dispose();
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
