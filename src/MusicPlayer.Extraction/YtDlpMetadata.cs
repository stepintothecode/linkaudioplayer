using System.Text.Json.Serialization;

namespace MusicPlayer.Extraction;

/// <summary>Subset of the JSON document yt-dlp emits with -J.</summary>
public sealed class YtDlpMetadata
{
    /// <summary>Video or track title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Track name when the source exposes music metadata.</summary>
    [JsonPropertyName("track")]
    public string? Track { get; set; }

    /// <summary>Artist name when the source exposes music metadata.</summary>
    [JsonPropertyName("artist")]
    public string? Artist { get; set; }

    /// <summary>Channel or account that published the media.</summary>
    [JsonPropertyName("uploader")]
    public string? Uploader { get; set; }

    /// <summary>Length in seconds.</summary>
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    /// <summary>Preferred artwork URL.</summary>
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    /// <summary>Canonical page URL for the media.</summary>
    [JsonPropertyName("webpage_url")]
    public string? WebpageUrl { get; set; }

    /// <summary>Extractor that handled the URL, for example "youtube" or "soundcloud".</summary>
    [JsonPropertyName("extractor")]
    public string? Extractor { get; set; }
}
