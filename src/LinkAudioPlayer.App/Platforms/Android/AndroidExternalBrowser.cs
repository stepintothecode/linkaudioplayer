using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.App.Platforms.Android;

/// <summary>Opens links in the device browser, never in an embedded web view.</summary>
public sealed class AndroidExternalBrowser : IExternalBrowser
{
    /// <inheritdoc />
    public async Task OpenAsync(string url, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        // External, not SystemPreferred. A support page inside the app reads as an in
        // app purchase that skipped store billing, which gets an app rejected.
        await Browser.Default.OpenAsync(url, BrowserLaunchMode.External);
    }
}
