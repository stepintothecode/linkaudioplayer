using MusicPlayer.Domain;

namespace MusicPlayer.Abstractions;

/// <summary>Persistence operations for tracks in the library.</summary>
public interface ITrackStore
{
    /// <summary>Returns every track, newest first.</summary>
    Task<IReadOnlyList<Track>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Returns one track, or null when missing.</summary>
    Task<Track?> GetAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>Returns the track already stored for a source URL, or null.</summary>
    Task<Track?> FindBySourceUrlAsync(string sourceUrl, CancellationToken cancellationToken);

    /// <summary>Inserts a track and returns it.</summary>
    Task<Track> AddAsync(Track track, CancellationToken cancellationToken);

    /// <summary>Deletes a track and its playlist membership rows. Returns false when missing.</summary>
    Task<bool> DeleteAsync(Guid trackId, CancellationToken cancellationToken);
}
