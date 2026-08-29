namespace MusicPlayer.Contracts;

/// <summary>Body for creating a playlist.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional description.</param>
public sealed record CreatePlaylistRequest(string Name, string? Description);

/// <summary>Body for renaming a playlist.</summary>
/// <param name="Name">New display name.</param>
/// <param name="Description">New description.</param>
public sealed record UpdatePlaylistRequest(string Name, string? Description);

/// <summary>Body for adding a track to the library from a URL.</summary>
/// <param name="Url">Source URL to resolve.</param>
public sealed record AddTrackRequest(string Url);

/// <summary>Body for adding an existing library track to a playlist.</summary>
/// <param name="TrackId">Track to append.</param>
public sealed record AddPlaylistTrackRequest(Guid TrackId);

/// <summary>Body for reordering a playlist.</summary>
/// <param name="ItemIds">Membership row identifiers in the desired order.</param>
public sealed record ReorderPlaylistRequest(IReadOnlyList<Guid> ItemIds);
