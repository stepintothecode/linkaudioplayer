namespace LinkAudioPlayer.Abstractions;

/// <summary>Metadata a resolver extracted from a source URL.</summary>
/// <param name="SourceUrl">URL that was resolved.</param>
/// <param name="Provider">Resolver name.</param>
/// <param name="Title">Display title.</param>
/// <param name="Artist">Uploader or artist, when reported.</param>
/// <param name="Duration">Track length, when reported.</param>
/// <param name="ThumbnailUrl">Remote artwork URL.</param>
/// <param name="IsPlayable">False for metadata only results, such as DRM hosts resolved for search purposes.</param>
public sealed record MediaResolution(
    string SourceUrl,
    string Provider,
    string Title,
    string? Artist,
    TimeSpan? Duration,
    string? ThumbnailUrl,
    bool IsPlayable = true);
