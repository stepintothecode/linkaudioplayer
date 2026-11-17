using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using LinkAudioPlayer.Ui.Services;
using AndroidUri = Android.Net.Uri;

namespace LinkAudioPlayer.App.Platforms.Android;

/// <summary>
/// Lists LinkAudioPlayer under "Open with" for audio files, and in the Share menu for
/// them. The file plays straight away; it is not added to the library, because the
/// app never takes ownership of the phone's files.
/// </summary>
/// <remarks>
/// The sender grants read access to this activity only. The grant is passed on to
/// MainActivity with the intent that opens it, or it would lapse when this one finishes
/// and the player could not open the file.
/// </remarks>
[Activity(
    Name = "com.kartikeyan.linkaudioplayer.OpenAudioActivity",
    Label = "LinkAudioPlayer",
    Exported = true,
    NoHistory = true,
    ExcludeFromRecents = true,
    TaskAffinity = "",
    Theme = "@android:style/Theme.Translucent.NoTitleBar")]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault],
    DataMimeType = "audio/*",
    DataSchemes = ["content", "file"])]
[IntentFilter(
    [Intent.ActionSend],
    Categories = [Intent.CategoryDefault],
    DataMimeType = "audio/*")]
public sealed class OpenAudioActivity : Activity
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

    /// <summary>Hands the file over and opens the app. See ShareActivity for why TaskAffinity is empty.</summary>
    private void Receive(Intent? intent)
    {
        var uri = intent?.Action == Intent.ActionSend ? SharedStream(intent) : intent?.Data;
        var inbox = IPlatformApplication.Current?.Services.GetService<OpenedAudioInbox>();

        if (uri is null || inbox is null)
        {
            global::Android.Widget.Toast.MakeText(this, "That file could not be opened", global::Android.Widget.ToastLength.Short)?.Show();
            Finish();
            return;
        }

        // An old style file:// link plays from its path; anything else from the URI.
        var location = uri.Scheme == "file" ? uri.Path ?? uri.ToString()! : uri.ToString()!;
        inbox.Deliver(new OpenedAudio(location, TitleOf(uri)));

        var open = new Intent(this, typeof(MainActivity));
        open.SetData(uri);
        open.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.GrantReadUriPermission);
        StartActivity(open);
        Finish();
    }

    /// <summary>The file attached to a share. The typed overload only exists from Android 13.</summary>
    private static AndroidUri? SharedStream(Intent intent) =>
        OperatingSystem.IsAndroidVersionAtLeast(33)
            ? intent.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(AndroidUri))) as AndroidUri
#pragma warning disable CA1422 // The replacement needs Android 13; this branch runs only below it.
            : intent.GetParcelableExtra(Intent.ExtraStream) as AndroidUri;
#pragma warning restore CA1422

    /// <summary>"Yellow.mp3" -> "Yellow". Falls back to the last path segment.</summary>
    private string TitleOf(AndroidUri uri)
    {
        string? name = null;

        try
        {
            using var cursor = ContentResolver?.Query(uri, [IOpenableColumns.DisplayName], null, null, null);
            if (cursor is not null && cursor.MoveToFirst())
            {
                name = cursor.GetString(0);
            }
        }
        catch (Java.Lang.Exception)
        {
            // Some providers refuse the query. The path still gives a name.
        }

        name ??= uri.LastPathSegment;
        var title = Path.GetFileNameWithoutExtension(name);
        return string.IsNullOrWhiteSpace(title) ? "Audio file" : title;
    }
}
