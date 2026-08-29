using Microsoft.EntityFrameworkCore;
using MusicPlayer.Abstractions;
using MusicPlayer.Domain;

namespace MusicPlayer.Data;

/// <summary>EF Core implementation of <see cref="IPlaylistStore"/>.</summary>
public sealed class PlaylistStore : IPlaylistStore
{
    private readonly LibraryDbContext _db;

    /// <summary>Creates the store over a library context.</summary>
    public PlaylistStore(LibraryDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Playlist>> ListAsync(CancellationToken cancellationToken) =>
        await _db.Playlists
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Playlist?> GetAsync(Guid playlistId, CancellationToken cancellationToken) =>
        await _db.Playlists
            .AsNoTracking()
            .Include(p => p.Items.OrderBy(i => i.Position))
            .ThenInclude(i => i.Track)
            .FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);

    /// <inheritdoc />
    public async Task<Playlist> CreateAsync(string name, string? description, CancellationToken cancellationToken)
    {
        var playlist = new Playlist { Name = name, Description = description };
        _db.Playlists.Add(playlist);
        await _db.SaveChangesAsync(cancellationToken);
        return playlist;
    }

    /// <inheritdoc />
    public async Task<bool> RenameAsync(
        Guid playlistId,
        string name,
        string? description,
        CancellationToken cancellationToken)
    {
        var playlist = await _db.Playlists.FindAsync([playlistId], cancellationToken);
        if (playlist is null)
        {
            return false;
        }

        playlist.Name = name;
        playlist.Description = description;
        playlist.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid playlistId, CancellationToken cancellationToken)
    {
        var playlist = await _db.Playlists.FindAsync([playlistId], cancellationToken);
        if (playlist is null)
        {
            return false;
        }

        _db.Playlists.Remove(playlist);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> AddTrackAsync(Guid playlistId, Guid trackId, CancellationToken cancellationToken)
    {
        var playlist = await _db.Playlists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);

        if (playlist is null || !await _db.Tracks.AnyAsync(t => t.Id == trackId, cancellationToken))
        {
            return false;
        }

        var nextPosition = playlist.Items.Count == 0 ? 0 : playlist.Items.Max(i => i.Position) + 1;

        var item = new PlaylistItem
        {
            PlaylistId = playlistId,
            TrackId = trackId,
            Position = nextPosition,
        };

        // Added explicitly rather than through playlist.Items. The entity carries a
        // pre-set Id, which EF reads as an existing row and turns into an UPDATE.
        _db.PlaylistItems.Add(item);
        playlist.Items.Add(item);

        playlist.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveItemAsync(Guid playlistId, Guid itemId, CancellationToken cancellationToken)
    {
        var playlist = await _db.Playlists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);

        var item = playlist?.Items.FirstOrDefault(i => i.Id == itemId);
        if (playlist is null || item is null)
        {
            return false;
        }

        playlist.Items.Remove(item);
        _db.PlaylistItems.Remove(item);
        Renumber(playlist);

        playlist.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ReorderAsync(
        Guid playlistId,
        IReadOnlyList<Guid> itemIdsInOrder,
        CancellationToken cancellationToken)
    {
        var playlist = await _db.Playlists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);

        if (playlist is null)
        {
            return false;
        }

        // Ignore the request unless it names exactly the items the playlist holds.
        var known = playlist.Items.Select(i => i.Id).ToHashSet();
        if (itemIdsInOrder.Count != known.Count || !itemIdsInOrder.All(known.Contains))
        {
            return false;
        }

        for (var position = 0; position < itemIdsInOrder.Count; position++)
        {
            var item = playlist.Items.First(i => i.Id == itemIdsInOrder[position]);
            item.Position = position;
        }

        playlist.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Rewrites positions to a dense zero based sequence.</summary>
    private static void Renumber(Playlist playlist)
    {
        var ordered = playlist.Items.OrderBy(i => i.Position).ToList();
        for (var position = 0; position < ordered.Count; position++)
        {
            ordered[position].Position = position;
        }
    }
}
