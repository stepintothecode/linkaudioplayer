namespace LinkAudioPlayer.Storage;

/// <summary>Where cached audio is written. The host sets this to a platform data directory.</summary>
public sealed class MediaCacheOptions
{
    /// <summary>Absolute path to the cache directory.</summary>
    public string Directory { get; set; } = "media";
}
