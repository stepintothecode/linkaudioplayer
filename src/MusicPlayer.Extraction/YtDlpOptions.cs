namespace MusicPlayer.Extraction;

/// <summary>Configuration for the yt-dlp subprocess. Bound from the "YtDlp" config section.</summary>
public sealed class YtDlpOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "YtDlp";

    /// <summary>Executable name or absolute path. Resolved from PATH when not absolute.</summary>
    public string ExecutablePath { get; set; } = "yt-dlp";

    /// <summary>Format selector passed to -f. Audio only by default to keep downloads small.</summary>
    public string AudioFormat { get; set; } = "bestaudio/best";

    /// <summary>Seconds before a metadata probe is abandoned.</summary>
    public int MetadataTimeoutSeconds { get; set; } = 60;

    /// <summary>Seconds before a download is abandoned.</summary>
    public int DownloadTimeoutSeconds { get; set; } = 600;
}
