using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.App.Platforms.Android;

/// <summary>
/// Sends the app to the background, which is what Android itself does for back on a
/// root screen. Finishing the activity instead would tear down the web view, and a
/// music app that stops the music on back defeats the point of background playback.
/// </summary>
public sealed class AndroidAppExit : IAppExit
{
    /// <inheritdoc />
    public void Leave() => MainThread.BeginInvokeOnMainThread(() => Platform.CurrentActivity?.MoveTaskToBack(true));
}
