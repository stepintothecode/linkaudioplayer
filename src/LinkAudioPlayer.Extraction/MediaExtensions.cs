namespace LinkAudioPlayer.Extraction;

/// <summary>File extensions treated as playable media.</summary>
public static class MediaExtensions
{
    private static readonly string[] Known =
        [".mp3", ".m4a", ".aac", ".ogg", ".oga", ".opus", ".flac", ".wav", ".webm", ".mp4", ".m4b"];

    /// <summary>True when the path ends with a known media extension.</summary>
    public static bool IsMediaPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var extension = Path.GetExtension(path);
        return Known.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
