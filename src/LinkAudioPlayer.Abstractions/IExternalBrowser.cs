namespace LinkAudioPlayer.Abstractions;

/// <summary>
/// Opens a link in the device browser rather than inside the app.
/// This is deliberate: a payment or support page shown in an embedded web view reads
/// as an in app purchase that skipped store billing, and gets an app rejected.
/// </summary>
public interface IExternalBrowser
{
    /// <summary>Opens the URL outside the app.</summary>
    Task OpenAsync(string url, CancellationToken cancellationToken);
}
