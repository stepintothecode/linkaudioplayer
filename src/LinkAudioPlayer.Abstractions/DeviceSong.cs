namespace LinkAudioPlayer.Abstractions;

/// <summary>A music file the phone's media index reports.</summary>
/// <param name="Uri">Content URI the file plays from. Its identity across scans.</param>
/// <param name="Title">Title tag, or the file name when there is none.</param>
/// <param name="Artist">Artist tag, when present.</param>
/// <param name="Duration">Length, when known.</param>
/// <param name="FolderPath">Folder relative to the storage root, such as "Music/Coldplay".</param>
public sealed record DeviceSong(
    string Uri,
    string Title,
    string? Artist,
    TimeSpan? Duration,
    string FolderPath);
