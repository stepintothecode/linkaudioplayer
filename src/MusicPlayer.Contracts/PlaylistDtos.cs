namespace MusicPlayer.Contracts;

/// <summary>Playlist without its contents, for list views.</summary>
/// <param name="Id">Playlist identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="TrackCount">Number of tracks held.</param>
public sealed record PlaylistSummaryDto(Guid Id, string Name, string? Description, int TrackCount);

/// <summary>One entry in a playlist, pairing a membership row with its track.</summary>
/// <param name="ItemId">Membership row identifier, used for removal and reordering.</param>
/// <param name="Position">Zero based sort order.</param>
/// <param name="Track">The referenced track.</param>
public sealed record PlaylistEntryDto(Guid ItemId, int Position, TrackDto Track);

/// <summary>Playlist with its ordered contents.</summary>
/// <param name="Id">Playlist identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Entries">Tracks in play order.</param>
public sealed record PlaylistDetailDto(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<PlaylistEntryDto> Entries);
