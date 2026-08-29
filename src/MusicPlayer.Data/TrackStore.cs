using Microsoft.EntityFrameworkCore;
using MusicPlayer.Abstractions;
using MusicPlayer.Domain;

namespace MusicPlayer.Data;

/// <summary>EF Core implementation of <see cref="ITrackStore"/>.</summary>
public sealed class TrackStore : ITrackStore
{
    private readonly LibraryDbContext _db;

    /// <summary>Creates the store over a library context.</summary>
    public TrackStore(LibraryDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Track>> ListAsync(CancellationToken cancellationToken) =>
        await _db.Tracks
            .AsNoTracking()
            .OrderByDescending(t => t.AddedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Track?> GetAsync(Guid trackId, CancellationToken cancellationToken) =>
        await _db.Tracks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == trackId, cancellationToken);

    /// <inheritdoc />
    public async Task<Track?> FindBySourceUrlAsync(string sourceUrl, CancellationToken cancellationToken) =>
        await _db.Tracks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.SourceUrl == sourceUrl, cancellationToken);

    /// <inheritdoc />
    public async Task<Track> AddAsync(Track track, CancellationToken cancellationToken)
    {
        _db.Tracks.Add(track);
        await _db.SaveChangesAsync(cancellationToken);
        return track;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var track = await _db.Tracks.FindAsync([trackId], cancellationToken);
        if (track is null)
        {
            return false;
        }

        _db.Tracks.Remove(track);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
