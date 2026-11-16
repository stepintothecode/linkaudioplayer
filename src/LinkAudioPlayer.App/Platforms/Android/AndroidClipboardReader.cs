using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.App.Platforms.Android;

/// <summary>Reads the clipboard through MAUI, which Android allows while the app is in front.</summary>
public sealed class AndroidClipboardReader : IClipboardReader
{
    /// <inheritdoc />
    public Task<string?> ReadTextAsync() =>
        MainThread.InvokeOnMainThreadAsync(() => Clipboard.Default.GetTextAsync());
}
