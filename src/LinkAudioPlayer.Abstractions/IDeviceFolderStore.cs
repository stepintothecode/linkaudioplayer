using LinkAudioPlayer.Domain;

namespace LinkAudioPlayer.Abstractions;

/// <summary>A folder with how many songs it holds.</summary>
/// <param name="Folder">The folder.</param>
/// <param name="SongCount">Songs currently in it.</param>
public sealed record DeviceFolderSummary(DeviceFolder Folder, int SongCount);

/// <summary>Persists the phone's music folders and the order the user gave them.</summary>
public interface IDeviceFolderStore
{
    /// <summary>Every folder that holds at least one song, in the user's order.</summary>
    Task<IReadOnlyList<DeviceFolderSummary>> ListAsync(CancellationToken cancellationToken);

    /// <summary>One folder, or null when it is gone.</summary>
    Task<DeviceFolder?> GetAsync(Guid folderId, CancellationToken cancellationToken);

    /// <summary>Songs in a folder, in the user's order.</summary>
    Task<IReadOnlyList<Track>> ListSongsAsync(Guid folderId, CancellationToken cancellationToken);

    /// <summary>Every phone song, grouped by folder in folder order.</summary>
    Task<IReadOnlyList<Track>> ListAllSongsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Brings the stored songs in line with a fresh scan. New songs join the end of their
    /// folder, vanished ones are removed along with their playlist entries, and existing
    /// ones keep their place. Returns true when anything changed.
    /// </summary>
    Task<bool> SyncAsync(IReadOnlyList<DeviceSong> scanned, CancellationToken cancellationToken);

    /// <summary>Reorders the folders. Returns false on a mismatch.</summary>
    Task<bool> ReorderFoldersAsync(IReadOnlyList<Guid> folderIdsInOrder, CancellationToken cancellationToken);

    /// <summary>Reorders the songs inside one folder. Returns false on a mismatch.</summary>
    Task<bool> ReorderSongsAsync(
        Guid folderId,
        IReadOnlyList<Guid> trackIdsInOrder,
        CancellationToken cancellationToken);
}
