namespace LinkAudioPlayer.Abstractions;

/// <summary>
/// Reads text from the device clipboard. Native rather than JavaScript, because an
/// Android web view refuses navigator.clipboard.readText.
/// </summary>
public interface IClipboardReader
{
    /// <summary>Clipboard text, or null when it is empty or not text.</summary>
    Task<string?> ReadTextAsync();
}
