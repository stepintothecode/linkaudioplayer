namespace MusicPlayer.Domain;

/// <summary>A single playable item resolved from a source URL.</summary>
public sealed class Track
{
    /// <summary>Stable local identifier.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Original URL the user supplied.</summary>
    public required string SourceUrl { get; set; }

    /// <summary>Resolver that produced this track, for example "yt-dlp" or "direct".</summary>
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

    /// <summary>Playlist rows referencing this track.</summary>
    public List<PlaylistItem> PlaylistItems { get; set; } = [];
}
