using Microsoft.EntityFrameworkCore;
using LinkAudioPlayer.Abstractions;
using LinkAudioPlayer.Domain;

namespace LinkAudioPlayer.Data;

/// <summary>
/// EF Core implementation of <see cref="ITrackStore"/>.
/// Takes a context per operation, since a long lived change tracker on a single
/// screen app causes stale reads and misclassified inserts.
/// </summary>
public sealed class TrackStore : ITrackStore
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;

    /// <summary>Creates the store over a context factory.</summary>
    public TrackStore(IDbContextFactory<LibraryDbContext> factory) => _factory = factory;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Track>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        return await db.Tracks
            .AsNoTracking()
            .OrderBy(t => t.Position)
            .ThenByDescending(t => t.AddedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Track?> GetAsync(Guid trackId, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        return await db.Tracks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == trackId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Track?> FindBySourceUrlAsync(string sourceUrl, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        return await db.Tracks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.SourceUrl == sourceUrl, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Track> AddAsync(Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        // One statement, so adding stays cheap however large the library grows.
        await db.Tracks.ExecuteUpdateAsync(
            setters => setters.SetProperty(t => t.Position, t => t.Position + 1),
            cancellationToken);

        track.Position = 0;
        db.Tracks.Add(track);
        await db.SaveChangesAsync(cancellationToken);
        return track;
    }

    /// <inheritdoc />
    public async Task<bool> ReorderAsync(
        IReadOnlyList<Guid> trackIdsInOrder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trackIdsInOrder);

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var tracks = await db.Tracks.ToListAsync(cancellationToken);

        // Ignore the request unless it names exactly the tracks the library holds.
        var known = tracks.Select(t => t.Id).ToHashSet();
        if (trackIdsInOrder.Count != known.Count || !trackIdsInOrder.All(known.Contains))
        {
            return false;
        }

        for (var position = 0; position < trackIdsInOrder.Count; position++)
        {
            tracks.First(t => t.Id == trackIdsInOrder[position]).Position = position;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid trackId, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var track = await db.Tracks.FindAsync([trackId], cancellationToken);
        if (track is null)
        {
            return false;
        }

        db.Tracks.Remove(track);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
