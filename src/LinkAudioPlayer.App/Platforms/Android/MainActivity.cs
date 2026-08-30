using Android.App;
using Android.Content.PM;
using Android.OS;

using AndroidX.Core.View;

namespace LinkAudioPlayer.App;

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

    /// <summary>Matches the web view background so the inset strips do not flash white.</summary>
    private static readonly Android.Graphics.Color Background = Android.Graphics.Color.ParseColor("#101014");

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        ApplyWindowInsets();
        RequestNotificationPermission();
    }

    /// <summary>
    /// Pads the content view by the system bar and cutout insets.
    /// Android 15 forces edge to edge, which otherwise puts the header under the status
    /// bar and the player under the navigation bar. Doing it here rather than in CSS
    /// avoids relying on env(safe-area-inset) support in the web view.
    /// </summary>
    private void ApplyWindowInsets()
    {
        var content = FindViewById(global::Android.Resource.Id.Content);
        if (content is null)
        {
            return;
        }

        content.SetBackgroundColor(Background);
        ViewCompat.SetOnApplyWindowInsetsListener(content, new InsetPadder());
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
            RequestPermissions([Android.Manifest.Permission.PostNotifications], NotificationPermissionRequest);
        }
    }

    /// <summary>Turns the reported insets into padding on the hosting view.</summary>
    private sealed class InsetPadder : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View? view, WindowInsetsCompat? insets)
        {
            if (view is null || insets is null)
            {
                return WindowInsetsCompat.Consumed!;
            }

            var bars = insets.GetInsets(
                WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout());

            if (bars is not null)
            {
                view.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            }

            return WindowInsetsCompat.Consumed!;
        }
    }
}
