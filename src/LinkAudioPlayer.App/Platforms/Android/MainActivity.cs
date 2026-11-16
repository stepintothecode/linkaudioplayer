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
    private static readonly Android.Graphics.Color Background = Android.Graphics.Color.ParseColor("#0d0b12");

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        ApplyWindowInsets();
        RequestNotificationPermission();

        _back = new AppBackCallback(this);
        _button = IPlatformApplication.Current?.Services.GetService<LinkAudioPlayer.Ui.Services.BackButton>();
        if (_button is not null)
        {
            _button.Attached += OnBackButtonAttached;
        }
    }

    private AppBackCallback? _back;
    private LinkAudioPlayer.Ui.Services.BackButton? _button;

    private void OnBackButtonAttached() => RunOnUiThread(RaiseBackCallback);

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        // The button outlives the activity, which Android recreates freely.
        if (_button is not null)
        {
            _button.Attached -= OnBackButtonAttached;
        }

        base.OnDestroy();
    }

    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        RaiseBackCallback();
    }

    /// <summary>
    /// Puts the app's back callback above every other. Android asks the most recently
    /// added callback first, and the web view adds its own, which walks its history,
    /// once it is created. MAUI's OnBackPressed lifecycle hook is not reached at all on
    /// Android 16, where back arrives through the predictive back dispatcher.
    /// </summary>
    private void RaiseBackCallback()
    {
        if (_back is null)
        {
            return;
        }

        _back.Remove();
        OnBackPressedDispatcher.AddCallback(this, _back);
    }

    /// <summary>Hands back presses to the app's rules, falling through to Android's default before they are ready.</summary>
    private sealed class AppBackCallback : AndroidX.Activity.OnBackPressedCallback
    {
        private readonly MainActivity _activity;

        public AppBackCallback(MainActivity activity)
            : base(true) => _activity = activity;

        public override void HandleOnBackPressed()
        {
            var button = IPlatformApplication.Current?.Services.GetService<LinkAudioPlayer.Ui.Services.BackButton>();
            if (button?.Press() == true)
            {
                return;
            }

            // Not ready yet: step aside for this one press so the default can run.
            Enabled = false;
            _activity.OnBackPressedDispatcher.OnBackPressed();
            Enabled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnActivityResult(int requestCode, Result resultCode, Android.Content.Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        LinkAudioPlayer.App.Platforms.Android.ActivityResults.Complete(requestCode, resultCode, data);
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
