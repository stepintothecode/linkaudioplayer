namespace LinkAudioPlayer.Abstractions;

/// <summary>Details the platform shows on the lock screen and notification shade.</summary>
/// <param name="Title">Track title.</param>
/// <param name="Artist">Artist or uploader.</param>
/// <param name="Duration">Track length, when known.</param>
/// <param name="ArtworkUrl">Remote artwork URL. The platform decides whether to fetch it.</param>
public sealed record NowPlaying(string Title, string? Artist, TimeSpan? Duration, string? ArtworkUrl);
