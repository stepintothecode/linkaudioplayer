using System.Text.Json.Serialization;

namespace LinkAudioPlayer.Backup;

/// <summary>
/// The on disk backup shape. Tracks are listed once and playlists reference them by
/// source URL, so the file stays readable and a link moved between playlists costs
/// nothing.
/// </summary>
public sealed class BackupDocument
{
    /// <summary>Current format version, raised only on a breaking change.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Identifies the file as ours before anything is read from it.</summary>
    [JsonPropertyName("app")]
    public string App { get; set; } = "LinkAudioPlayer";

    /// <summary>Format version.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = CurrentVersion;

    /// <summary>When the backup was taken.</summary>
    [JsonPropertyName("exportedAt")]
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Every track in the library.</summary>
    [JsonPropertyName("tracks")]
    public List<BackupTrack> Tracks { get; set; } = [];

    /// <summary>Every playlist, referencing tracks by source URL.</summary>
    [JsonPropertyName("playlists")]
    public List<BackupPlaylist> Playlists { get; set; } = [];
}

/// <summary>One library track in a backup.</summary>
public sealed class BackupTrack
{
    /// <summary>Original URL. This is the identity used when importing.</summary>
    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>Resolver that produced the track.</summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>Display title.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Artist or uploader.</summary>
    [JsonPropertyName("artist")]
    public string? Artist { get; set; }

    /// <summary>Length in seconds, when known.</summary>
    [JsonPropertyName("durationSeconds")]
    public double? DurationSeconds { get; set; }

    /// <summary>Remote artwork URL.</summary>
    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }
}

/// <summary>One playlist in a backup.</summary>
public sealed class BackupPlaylist
{
    /// <summary>Display name. This is the identity used when importing.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Track source URLs, in play order.</summary>
    [JsonPropertyName("trackUrls")]
    public List<string> TrackUrls { get; set; } = [];
}
