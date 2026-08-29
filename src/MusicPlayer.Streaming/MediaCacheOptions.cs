namespace MusicPlayer.Streaming;

/// <summary>Configuration for the on disk media cache. Bound from the "MediaCache" config section.</summary>
public sealed class MediaCacheOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "MediaCache";

    /// <summary>Directory holding cached audio. Relative paths resolve against the content root.</summary>
    public string Directory { get; set; } = "data/media";
}
