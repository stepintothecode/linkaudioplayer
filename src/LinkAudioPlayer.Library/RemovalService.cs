using LinkAudioPlayer.Abstractions;
using LinkAudioPlayer.Domain;

namespace LinkAudioPlayer.Library;

/// <summary>
/// Deletes a track or a playlist, returning enough to put it back exactly.
/// Undo has to restore position as well as existence, so the order of the library
/// and of every affected playlist is captured before anything is removed.
/// </summary>
public sealed class RemovalService
{
    private readonly ITrackStore _tracks;
    private readonly IPlaylistStore _playlists;

    /// <summary>Creates the service over the two stores.</summary>
    public RemovalService(ITrackStore tracks, IPlaylistStore playlists)
    {
        _tracks = tracks;
        _playlists = playlists;
    }

    /// <summary>Removes a track, returning a snapshot for undo, or null when it is gone.</summary>
    public async Task<RemovedTrack?> RemoveTrackAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var track = await _tracks.GetAsync(trackId, cancellationToken);
        if (track is null)
        {
            return null;
        }

        var libraryOrder = (await _tracks.ListAsync(cancellationToken)).Select(t => t.Id).ToList();

        var placements = (await _playlists.ListAsync(cancellationToken))
            .Where(playlist => playlist.Items.Any(item => item.TrackId == trackId))
            .Select(playlist => new PlaylistPlacement(
                playlist.Id,
                [.. playlist.Items.OrderBy(item => item.Position).Select(item => item.TrackId)]))
            .ToList();

        await _tracks.DeleteAsync(trackId, cancellationToken);
        return new RemovedTrack(track, libraryOrder, placements);
    }

    /// <summary>Puts a removed track back, in its old library position and playlists.</summary>
    public async Task RestoreAsync(RemovedTrack removed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(removed);

        await _tracks.AddAsync(removed.Track, cancellationToken);
        await _tracks.ReorderAsync(removed.LibraryOrder, cancellationToken);

        foreach (var placement in removed.Placements)
        {
            if (!await _playlists.AddTrackAsync(placement.PlaylistId, removed.Track.Id, cancellationToken))
            {
                continue;
            }

            await RestoreOrderAsync(placement, cancellationToken);
        }
    }

    /// <summary>Removes a playlist, returning a snapshot for undo, or null when it is gone.</summary>
    public async Task<RemovedPlaylist?> RemovePlaylistAsync(Guid playlistId, CancellationToken cancellationToken)
    {
        var playlist = await _playlists.GetAsync(playlistId, cancellationToken);
        if (playlist is null)
        {
            return null;
        }

        var order = playlist.Items
            .OrderBy(item => item.Position)
            .Select(item => item.TrackId)
            .ToList();

        await _playlists.DeleteAsync(playlistId, cancellationToken);
        return new RemovedPlaylist(playlist.Name, playlist.Description, order);
    }

    /// <summary>Recreates a removed playlist with its tracks in the same order.</summary>
    public async Task RestoreAsync(RemovedPlaylist removed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(removed);

        var created = await _playlists.CreateAsync(removed.Name, removed.Description, cancellationToken);
        if (created is null)
        {
            return;
        }

        // Added in order, so positions come out right without a reorder pass.
        foreach (var trackId in removed.TrackIds)
        {
            await _playlists.AddTrackAsync(created.Id, trackId, cancellationToken);
        }
    }

    /// <summary>
    /// Re-sorts a playlist to the captured track order.
    /// The restored track is appended by AddTrackAsync, so a reorder is needed to
    /// return it to the position it held before.
    /// </summary>
    private async Task RestoreOrderAsync(PlaylistPlacement placement, CancellationToken cancellationToken)
    {
        var playlist = await _playlists.GetAsync(placement.PlaylistId, cancellationToken);
        if (playlist is null)
        {
            return;
        }

        var itemsByTrack = playlist.Items
            .GroupBy(item => item.TrackId)
            .ToDictionary(group => group.Key, group => group.First().Id);

        var order = placement.TrackIds
            .Where(itemsByTrack.ContainsKey)
            .Select(trackId => itemsByTrack[trackId])
            .ToList();

        if (order.Count == playlist.Items.Count)
        {
            await _playlists.ReorderAsync(placement.PlaylistId, order, cancellationToken);
        }
    }
}

/// <summary>Where a track sat inside one playlist.</summary>
/// <param name="PlaylistId">The playlist.</param>
/// <param name="TrackIds">Its track ids, in play order, before the removal.</param>
public sealed record PlaylistPlacement(Guid PlaylistId, IReadOnlyList<Guid> TrackIds);

/// <summary>Everything needed to put a removed track back.</summary>
/// <param name="Track">The track itself.</param>
/// <param name="LibraryOrder">Library track ids in order, before the removal.</param>
/// <param name="Placements">The playlists it belonged to.</param>
public sealed record RemovedTrack(
    Track Track,
    IReadOnlyList<Guid> LibraryOrder,
    IReadOnlyList<PlaylistPlacement> Placements);

/// <summary>Everything needed to recreate a removed playlist.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Description">Its description.</param>
/// <param name="TrackIds">Its track ids, in play order.</param>
public sealed record RemovedPlaylist(string Name, string? Description, IReadOnlyList<Guid> TrackIds);
