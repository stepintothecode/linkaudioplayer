using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MusicPlayer.App;

/// <summary>Single activity hosting the Blazor web view.</summary>
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity
{
    private const int NotificationPermissionRequest = 1000;

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestNotificationPermission();
    }

    /// <summary>
    /// Android 13 and later need runtime consent before the playback notification shows.
    /// Without it the foreground service starts but the lock screen controls stay hidden.
    /// </summary>
    private void RequestNotificationPermission()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return;
        }

        if (CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions(
                [Android.Manifest.Permission.PostNotifications],
                NotificationPermissionRequest);
        }
    }
}
