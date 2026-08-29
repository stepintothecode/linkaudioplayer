namespace MusicPlayer.Contracts;

/// <summary>Track as returned by the API.</summary>
/// <param name="Id">Track identifier.</param>
/// <param name="Title">Display title.</param>
/// <param name="Artist">Artist or uploader.</param>
/// <param name="DurationSeconds">Length in seconds, when known.</param>
/// <param name="ThumbnailUrl">Artwork URL for lock screen metadata.</param>
/// <param name="SourceUrl">Original URL the track was added from.</param>
/// <param name="Provider">Resolver that produced the track.</param>
/// <param name="IsCached">True when audio is already on disk and playback starts immediately.</param>
public sealed record TrackDto(
    Guid Id,
    string Title,
    string? Artist,
    double? DurationSeconds,
    string? ThumbnailUrl,
    string SourceUrl,
    string Provider,
    bool IsCached);
