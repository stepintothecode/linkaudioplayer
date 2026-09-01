using Microsoft.EntityFrameworkCore;
using LinkAudioPlayer.Abstractions;
using LinkAudioPlayer.Domain;

namespace LinkAudioPlayer.Data;

/// <summary>
/// EF Core implementation of <see cref="IPlaylistStore"/>.
/// Takes a context per operation, for the reasons noted on <see cref="TrackStore"/>.
/// </summary>
public sealed class PlaylistStore : IPlaylistStore
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;

    /// <summary>Creates the store over a context factory.</summary>
    public PlaylistStore(IDbContextFactory<LibraryDbContext> factory) => _factory = factory;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Playlist>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        return await db.Playlists
            .AsNoTracking()
            .Include(p => p.Items)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Playlist?> GetAsync(Guid playlistId, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        return await db.Playlists
            .AsNoTracking()
            .Include(p => p.Items.OrderBy(i => i.Position))
            .ThenInclude(i => i.Track)
            .FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Playlist?> CreateAsync(string name, string? description, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var trimmed = name.Trim();
        if (await ExistsAsync(db, trimmed, excluding: null, cancellationToken))
        {
            return null;
        }

        var playlist = new Playlist { Name = trimmed, Description = description };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(cancellationToken);
        return playlist;
    }

    /// <inheritdoc />
    public async Task<bool> NameExistsAsync(string name, Guid? excluding, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await ExistsAsync(db, name.Trim(), excluding, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RenameAsync(
        Guid playlistId,
        string name,
        string? description,
        CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var trimmed = name.Trim();
        var playlist = await db.Playlists.FindAsync([playlistId], cancellationToken);

        if (playlist is null || await ExistsAsync(db, trimmed, playlistId, cancellationToken))
        {
            return false;
        }

        playlist.Name = trimmed;
        playlist.Description = description;
        playlist.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid playlistId, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var playlist = await db.Playlists.FindAsync([playlistId], cancellationToken);
        if (playlist is null)
        {
            return false;
        }

        db.Playlists.Remove(playlist);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> AddTrackAsync(Guid playlistId, Guid trackId, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var playlist = await db.Playlists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);

        if (playlist is null || !await db.Tracks.AnyAsync(t => t.Id == trackId, cancellationToken))
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
        db.PlaylistItems.Add(item);
        playlist.Items.Add(item);

        playlist.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveItemAsync(Guid playlistId, Guid itemId, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var playlist = await db.Playlists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == playlistId, cancellationToken);

        var item = playlist?.Items.FirstOrDefault(i => i.Id == itemId);
        if (playlist is null || item is null)
        {
            return false;
        }

        playlist.Items.Remove(item);
        db.PlaylistItems.Remove(item);
        Renumber(playlist);

        playlist.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ReorderAsync(
        Guid playlistId,
        IReadOnlyList<Guid> itemIdsInOrder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(itemIdsInOrder);

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var playlist = await db.Playlists
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
            playlist.Items.First(i => i.Id == itemIdsInOrder[position]).Position = position;
        }

        playlist.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// True when another playlist already uses the name.
    /// SQLite compares strings case sensitively by default, so the comparison is done
    /// on lowercased values rather than relying on the collation.
    /// </summary>
    private static async Task<bool> ExistsAsync(
        LibraryDbContext db,
        string name,
        Guid? excluding,
        CancellationToken cancellationToken)
    {
        var lowered = name.ToLowerInvariant();

        return await db.Playlists
            .AsNoTracking()
            .AnyAsync(
                p => p.Name.ToLower() == lowered && (excluding == null || p.Id != excluding),
                cancellationToken);
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
