namespace LinkAudioPlayer.Domain;

/// <summary>A single playable item resolved from a source URL.</summary>
public sealed class Track
{
    /// <summary>Stable local identifier.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Original URL the user supplied.</summary>
    public required string SourceUrl { get; set; }

    /// <summary>Resolver that produced this track, for example "youtube" or "direct".</summary>
    public required string Provider { get; set; }

    /// <summary>Display title.</summary>
    public required string Title { get; set; }

    /// <summary>Uploader or artist, when the source reports one.</summary>
    public string? Artist { get; set; }

    /// <summary>Duration in seconds, or null when unknown.</summary>
    public double? DurationSeconds { get; set; }

    /// <summary>Remote artwork URL used for lock screen metadata.</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>Cache key used to locate downloaded audio on disk.</summary>
    public required string CacheKey { get; set; }

    /// <summary>When the track was added to the library.</summary>
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Sort order in the library, ascending, dense from zero.
    /// A new track takes position zero and pushes the rest down, so the library reads
    /// newest first until the user drags something. For a song on the phone it is the
    /// order within its folder instead.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Folder holding a song found on the phone, such as "Music/Coldplay". Null for a
    /// track added from a link. A phone song plays straight from its content URI and is
    /// never downloaded, deleted or shown in the library.
    /// </summary>
    public string? FolderPath { get; set; }

    /// <summary>True for a song found on the phone rather than added from a link.</summary>
    public bool IsOnDevice => FolderPath is not null;

    /// <summary>Provider name given to songs found on the phone.</summary>
    public const string DeviceProvider = "device";

    /// <summary>Playlist rows referencing this track.</summary>
    public List<PlaylistItem> PlaylistItems { get; set; } = [];
}
