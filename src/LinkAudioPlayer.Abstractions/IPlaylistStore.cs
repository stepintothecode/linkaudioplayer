using LinkAudioPlayer.Domain;

namespace LinkAudioPlayer.Abstractions;

/// <summary>Persistence operations for playlists and their ordered contents.</summary>
public interface IPlaylistStore
{
    /// <summary>Returns every playlist without loading its items.</summary>
    Task<IReadOnlyList<Playlist>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Returns one playlist with its items and tracks loaded in order, or null when missing.</summary>
    Task<Playlist?> GetAsync(Guid playlistId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a playlist and returns it, or null when the name is already taken.
    /// Names are compared trimmed and case insensitively.
    /// </summary>
    Task<Playlist?> CreateAsync(string name, string? description, CancellationToken cancellationToken);

    /// <summary>True when a playlist already uses the name, ignoring the excluded one.</summary>
    Task<bool> NameExistsAsync(string name, Guid? excluding, CancellationToken cancellationToken);

    /// <summary>Renames a playlist. Returns false when it is missing or the name is taken.</summary>
    Task<bool> RenameAsync(Guid playlistId, string name, string? description, CancellationToken cancellationToken);

    /// <summary>Deletes a playlist and its membership rows. Returns false when it does not exist.</summary>
    Task<bool> DeleteAsync(Guid playlistId, CancellationToken cancellationToken);

    /// <summary>Appends a track to the end of a playlist. Returns false when either side is missing.</summary>
    Task<bool> AddTrackAsync(Guid playlistId, Guid trackId, CancellationToken cancellationToken);

    /// <summary>Removes one membership row and closes the resulting gap in positions.</summary>
    Task<bool> RemoveItemAsync(Guid playlistId, Guid itemId, CancellationToken cancellationToken);

    /// <summary>Reorders a playlist to match the supplied item order.</summary>
    Task<bool> ReorderAsync(Guid playlistId, IReadOnlyList<Guid> itemIdsInOrder, CancellationToken cancellationToken);

    /// <summary>Reorders the playlists themselves. Returns false on a mismatch.</summary>
    Task<bool> ReorderPlaylistsAsync(IReadOnlyList<Guid> playlistIdsInOrder, CancellationToken cancellationToken);
}
