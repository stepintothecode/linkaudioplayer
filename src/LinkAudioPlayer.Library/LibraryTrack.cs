using LinkAudioPlayer.Domain;

namespace LinkAudioPlayer.Library;

/// <summary>A track paired with whether its audio is already on the device.</summary>
/// <param name="Track">The stored track.</param>
/// <param name="IsDownloaded">True when audio is on disk and playback starts instantly.</param>
public sealed record LibraryTrack(Track Track, bool IsDownloaded);
