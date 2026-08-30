using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Application = Android.App.Application;
using MediaStyle = AndroidX.Media.App.NotificationCompat.MediaStyle;

namespace LinkAudioPlayer.App.Platforms.Android;

/// <summary>
/// Foreground service that keeps the process alive during playback and owns the
/// media notification. Without it Android stops the app once the screen locks.
/// </summary>
[Service(
    Exported = false,
    ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback)]
public sealed class PlaybackService : Service
{
    private const string ChannelId = "playback";
    private const int NotificationId = 1;

    /// <summary>Starts the service in the foreground. Safe to call repeatedly.</summary>
    public static void Start()
    {
        using var intent = new Intent(Application.Context, typeof(PlaybackService));
        Application.Context.StartForegroundService(intent);
    }

    /// <summary>Redraws the notification after a transport or metadata change.</summary>
    public static void Refresh()
    {
        var notification = Build(Application.Context);
        if (notification is null)
        {
            return;
        }

        var manager = NotificationManagerCompat.From(Application.Context);
        manager?.Notify(NotificationId, notification);
    }

    /// <summary>Stops the service and removes the notification.</summary>
    public static void Stop()
    {
        using var intent = new Intent(Application.Context, typeof(PlaybackService));
        Application.Context.StopService(intent);
    }

    /// <inheritdoc />
    public override IBinder? OnBind(Intent? intent) => null;

    /// <inheritdoc />
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateChannel();

        var notification = Build(this);
        if (notification is not null)
        {
            StartForeground(NotificationId, notification);
        }

        // Restarting with no queue would show an empty notification, so do not redeliver.
        return StartCommandResult.NotSticky;
    }

    /// <inheritdoc />
    public override void OnDestroy()
    {
        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }

    /// <summary>Builds the media style notification bound to the active session.</summary>
    private static Notification? Build(Context context)
    {
        var player = AndroidAudioPlayer.Current;
        var nowPlaying = player?.NowPlaying;

        if (player is null || nowPlaying is null)
        {
            return null;
        }

        var style = new MediaStyle();
        style.SetMediaSession(player.SessionToken);
        style.SetShowActionsInCompactView(0, 1, 2);

        var builder = new NotificationCompat.Builder(context, ChannelId);
        builder.SetContentTitle(nowPlaying.Title);
        builder.SetContentText(nowPlaying.Artist ?? string.Empty);
        builder.SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay);
        builder.SetStyle(style);
        builder.SetVisibility(NotificationCompat.VisibilityPublic);
        builder.SetOngoing(player.IsPlaying);
        builder.SetContentIntent(LaunchIntent(context));

        return builder.Build();
    }

    /// <summary>Intent that reopens the app when the notification is tapped.</summary>
    private static PendingIntent? LaunchIntent(Context context)
    {
        var packageName = context.PackageName;
        if (packageName is null)
        {
            return null;
        }

        var launch = context.PackageManager?.GetLaunchIntentForPackage(packageName);
        return launch is null
            ? null
            : PendingIntent.GetActivity(context, 0, launch, PendingIntentFlags.Immutable);
    }

    /// <summary>Creates the notification channel. Required from Android 8 onwards.</summary>
    private static void CreateChannel()
    {
        using var channel = new NotificationChannel(ChannelId, "Playback", NotificationImportance.Low)
        {
            Description = "Media controls for the current track.",
        };

        channel.SetShowBadge(false);

        var manager = (NotificationManager?)Application.Context.GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(channel);
    }
}
