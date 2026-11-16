using Microsoft.EntityFrameworkCore;
using LinkAudioPlayer.Abstractions;
using LinkAudioPlayer.Domain;

namespace LinkAudioPlayer.Data;

/// <summary>
/// EF Core implementation of <see cref="IDeviceFolderStore"/>.
/// Takes a context per operation, for the reasons noted on <see cref="TrackStore"/>.
/// </summary>
public sealed class DeviceFolderStore : IDeviceFolderStore
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;

    /// <summary>Creates the store over a context factory.</summary>
    public DeviceFolderStore(IDbContextFactory<LibraryDbContext> factory) => _factory = factory;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeviceFolderSummary>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var counts = await db.Tracks
            .Where(t => t.FolderPath != null)
            .GroupBy(t => t.FolderPath!)
            .Select(g => new { Path = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Path, g => g.Count, cancellationToken);

        var folders = await db.DeviceFolders
            .AsNoTracking()
            .OrderBy(f => f.Position)
            .ToListAsync(cancellationToken);

        return
        [
            .. folders
                .Where(f => counts.ContainsKey(f.Path))
                .Select(f => new DeviceFolderSummary(f, counts[f.Path])),
        ];
    }

    /// <inheritdoc />
    public async Task<DeviceFolder?> GetAsync(Guid folderId, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        return await db.DeviceFolders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Track>> ListSongsAsync(Guid folderId, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var path = await db.DeviceFolders
            .Where(f => f.Id == folderId)
            .Select(f => f.Path)
            .FirstOrDefaultAsync(cancellationToken);

        if (path is null)
        {
            return [];
        }

        return await db.Tracks
            .AsNoTracking()
            .Where(t => t.FolderPath == path)
            .OrderBy(t => t.Position)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Track>> ListAllSongsAsync(CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var folderOrder = await db.DeviceFolders
            .ToDictionaryAsync(f => f.Path, f => f.Position, cancellationToken);

        var songs = await db.Tracks
            .AsNoTracking()
            .Where(t => t.FolderPath != null)
            .ToListAsync(cancellationToken);

        return
        [
            .. songs
                .OrderBy(t => folderOrder.GetValueOrDefault(t.FolderPath!, int.MaxValue))
                .ThenBy(t => t.FolderPath, StringComparer.Ordinal)
                .ThenBy(t => t.Position),
        ];
    }

    /// <inheritdoc />
    public async Task<bool> SyncAsync(IReadOnlyList<DeviceSong> scanned, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scanned);

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        // The media index can list one file twice across volumes. The first wins.
        var incoming = scanned
            .GroupBy(s => s.Uri, StringComparer.Ordinal)
            .Select(g => g.First() with { FolderPath = DeviceFolder.Normalise(g.First().FolderPath) })
            .ToList();

        var stored = await db.Tracks.Where(t => t.FolderPath != null).ToListAsync(cancellationToken);
        var byUri = stored.ToDictionary(t => t.SourceUrl, StringComparer.Ordinal);
        var incomingUris = incoming.Select(s => s.Uri).ToHashSet(StringComparer.Ordinal);

        var gone = stored.Where(t => !incomingUris.Contains(t.SourceUrl)).ToList();
        var touchedFolders = new HashSet<string>(StringComparer.Ordinal);
        var changed = await RemoveGoneAsync(db, gone, touchedFolders, cancellationToken);

        var kept = stored.Except(gone).ToList();
        var nextPosition = kept
            .GroupBy(t => t.FolderPath!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Max(t => t.Position) + 1, StringComparer.Ordinal);

        int TakePosition(string folder)
        {
            var position = nextPosition.GetValueOrDefault(folder);
            nextPosition[folder] = position + 1;
            return position;
        }

        foreach (var song in incoming.Where(s => byUri.ContainsKey(s.Uri)))
        {
            changed |= Update(byUri[song.Uri], song, touchedFolders, TakePosition);
        }

        // New songs join the end of their folder, alphabetically among themselves.
        var added = incoming
            .Where(s => !byUri.ContainsKey(s.Uri))
            .OrderBy(s => s.FolderPath, StringComparer.Ordinal)
            .ThenBy(s => s.Title, StringComparer.CurrentCultureIgnoreCase);

        foreach (var song in added)
        {
            db.Tracks.Add(new Track
            {
                SourceUrl = song.Uri,
                Provider = Track.DeviceProvider,
                Title = song.Title,
                Artist = song.Artist,
                DurationSeconds = song.Duration?.TotalSeconds,
                CacheKey = CacheKeyFactory.FromSourceUrl(song.Uri),
                FolderPath = song.FolderPath,
                Position = TakePosition(song.FolderPath),
            });

            changed = true;
        }

        changed |= await SyncFoldersAsync(db, incoming, cancellationToken);

        if (!changed)
        {
            return false;
        }

        await db.SaveChangesAsync(cancellationToken);
        await RenumberSongsAsync(db, touchedFolders, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ReorderFoldersAsync(
        IReadOnlyList<Guid> folderIdsInOrder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folderIdsInOrder);

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var folders = await db.DeviceFolders.ToListAsync(cancellationToken);

        var known = folders.Select(f => f.Id).ToHashSet();
        if (folderIdsInOrder.Count != known.Count || !folderIdsInOrder.All(known.Contains))
        {
            return false;
        }

        var byId = folders.ToDictionary(f => f.Id);
        for (var position = 0; position < folderIdsInOrder.Count; position++)
        {
            byId[folderIdsInOrder[position]].Position = position;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ReorderSongsAsync(
        Guid folderId,
        IReadOnlyList<Guid> trackIdsInOrder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trackIdsInOrder);

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var path = await db.DeviceFolders
            .Where(f => f.Id == folderId)
            .Select(f => f.Path)
            .FirstOrDefaultAsync(cancellationToken);

        if (path is null)
        {
            return false;
        }

        var songs = await db.Tracks.Where(t => t.FolderPath == path).ToListAsync(cancellationToken);

        var known = songs.Select(t => t.Id).ToHashSet();
        if (trackIdsInOrder.Count != known.Count || !trackIdsInOrder.All(known.Contains))
        {
            return false;
        }

        var byId = songs.ToDictionary(t => t.Id);
        for (var position = 0; position < trackIdsInOrder.Count; position++)
        {
            byId[trackIdsInOrder[position]].Position = position;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Deletes songs whose files are gone, with their playlist entries, then closes the
    /// gaps those entries leave in each playlist.
    /// </summary>
    private static async Task<bool> RemoveGoneAsync(
        LibraryDbContext db,
        List<Track> gone,
        HashSet<string> touchedFolders,
        CancellationToken cancellationToken)
    {
        if (gone.Count == 0)
        {
            return false;
        }

        var goneIds = gone.Select(t => t.Id).ToList();
        var entries = await db.PlaylistItems
            .Where(i => goneIds.Contains(i.TrackId))
            .ToListAsync(cancellationToken);

        var playlistIds = entries.Select(i => i.PlaylistId).Distinct().ToList();

        db.PlaylistItems.RemoveRange(entries);
        db.Tracks.RemoveRange(gone);
        touchedFolders.UnionWith(gone.Select(t => t.FolderPath!));

        if (playlistIds.Count > 0)
        {
            var survivors = await db.PlaylistItems
                .Where(i => playlistIds.Contains(i.PlaylistId) && !goneIds.Contains(i.TrackId))
                .ToListAsync(cancellationToken);

            foreach (var playlist in survivors.GroupBy(i => i.PlaylistId))
            {
                var position = 0;
                foreach (var item in playlist.OrderBy(i => i.Position))
                {
                    item.Position = position++;
                }
            }
        }

        return true;
    }

    /// <summary>Applies a rescan to a stored song. True when anything differed.</summary>
    private static bool Update(
        Track track,
        DeviceSong song,
        HashSet<string> touchedFolders,
        Func<string, int> takePosition)
    {
        var duration = song.Duration?.TotalSeconds;
        var changed = false;

        if (!string.Equals(track.FolderPath, song.FolderPath, StringComparison.Ordinal))
        {
            // Moved to another folder: it joins the end there.
            touchedFolders.Add(track.FolderPath!);
            track.FolderPath = song.FolderPath;
            track.Position = takePosition(song.FolderPath);
            changed = true;
        }

        if (track.Title != song.Title || track.Artist != song.Artist || track.DurationSeconds != duration)
        {
            track.Title = song.Title;
            track.Artist = song.Artist;
            track.DurationSeconds = duration;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Keeps one folder row per path that holds songs. New folders join the end,
    /// alphabetically among themselves, and empty ones are dropped.
    /// </summary>
    private static async Task<bool> SyncFoldersAsync(
        LibraryDbContext db,
        List<DeviceSong> incoming,
        CancellationToken cancellationToken)
    {
        var folders = await db.DeviceFolders.OrderBy(f => f.Position).ToListAsync(cancellationToken);
        var live = incoming.Select(s => s.FolderPath).ToHashSet(StringComparer.Ordinal);
        var existing = folders.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);

        var empty = folders.Where(f => !live.Contains(f.Path)).ToList();
        var fresh = live
            .Where(path => !existing.Contains(path))
            .Select(path => new DeviceFolder { Path = path, Name = DeviceFolder.NameFor(path) })
            .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(f => f.Path, StringComparer.Ordinal)
            .ToList();

        if (empty.Count == 0 && fresh.Count == 0)
        {
            return false;
        }

        db.DeviceFolders.RemoveRange(empty);
        db.DeviceFolders.AddRange(fresh);

        var position = 0;
        foreach (var folder in folders.Except(empty).Concat(fresh))
        {
            folder.Position = position++;
        }

        return true;
    }

    /// <summary>Closes the gaps left in folders that lost songs.</summary>
    private static async Task RenumberSongsAsync(
        LibraryDbContext db,
        HashSet<string> folders,
        CancellationToken cancellationToken)
    {
        if (folders.Count == 0)
        {
            return;
        }

        var paths = folders.ToList();
        var songs = await db.Tracks
            .Where(t => t.FolderPath != null && paths.Contains(t.FolderPath))
            .ToListAsync(cancellationToken);

        foreach (var folder in songs.GroupBy(t => t.FolderPath))
        {
            var position = 0;
            foreach (var song in folder.OrderBy(t => t.Position))
            {
                song.Position = position++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
