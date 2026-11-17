using Android.App;
using Android.Content;
using Android.OS;
using LinkAudioPlayer.Ui.Services;

namespace LinkAudioPlayer.App.Platforms.Android;

/// <summary>
/// Puts LinkAudioPlayer in the Share menu of YouTube and any other app that shares text.
/// It shows nothing: it hands the link to the app and opens the main screen.
/// A separate activity, because a share launches into the sharing app's task, and
/// receiving it in MainActivity would start a second copy of the app inside YouTube.
/// </summary>
[Activity(
    Name = "com.kartikeyan.linkaudioplayer.ShareActivity",
    Label = "LinkAudioPlayer",
    Exported = true,
    NoHistory = true,
    ExcludeFromRecents = true,
    TaskAffinity = "",
    Theme = "@android:style/Theme.Translucent.NoTitleBar")]
[IntentFilter(
    [Intent.ActionSend],
    Categories = [Intent.CategoryDefault],
    DataMimeType = "text/plain")]
public sealed class ShareActivity : Activity
{
    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Receive(Intent);
    }

    /// <inheritdoc />
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Receive(intent);
    }

    /// <summary>
    /// Hands the link over and opens the app. TaskAffinity is empty so this never
    /// becomes the root of the app's own task: when it was, a second share launched as a
    /// new task only brought that task forward, and the link never arrived.
    /// </summary>
    private void Receive(Intent? intent)
    {
        var text = intent?.GetStringExtra(Intent.ExtraText);
        var inbox = IPlatformApplication.Current?.Services.GetService<SharedLinkInbox>();

        if (inbox?.Deliver(text) != true)
        {
            global::Android.Widget.Toast.MakeText(this, "That share had no link in it", global::Android.Widget.ToastLength.Short)?.Show();
            Finish();
            return;
        }

        // Brings the running app forward in its own task, or starts it.
        var open = new Intent(this, typeof(MainActivity));
        open.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        StartActivity(open);
        Finish();
    }
}
