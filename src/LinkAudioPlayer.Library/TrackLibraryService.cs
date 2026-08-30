using LinkAudioPlayer.Abstractions;
using LinkAudioPlayer.Domain;
using LinkAudioPlayer.Extraction;

namespace LinkAudioPlayer.Library;

/// <summary>Adds, downloads and removes tracks. The UI talks to this rather than to the stores.</summary>
public sealed class TrackLibraryService
{
    private readonly MediaResolutionService _resolution;
    private readonly ITrackStore _tracks;
    private readonly IMediaCache _cache;

    /// <summary>Creates the service over resolution, persistence and the media cache.</summary>
    public TrackLibraryService(
        MediaResolutionService resolution,
        ITrackStore tracks,
        IMediaCache cache)
    {
        _resolution = resolution;
        _tracks = tracks;
        _cache = cache;
    }

    /// <summary>Returns every track, newest first, with its download state.</summary>
    public async Task<IReadOnlyList<LibraryTrack>> ListAsync(CancellationToken cancellationToken)
    {
        var tracks = await _tracks.ListAsync(cancellationToken);
        return [.. tracks.Select(Describe)];
    }

    /// <summary>Returns one track with its download state, or null when missing.</summary>
    public async Task<LibraryTrack?> GetAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var track = await _tracks.GetAsync(trackId, cancellationToken);
        return track is null ? null : Describe(track);
    }

    /// <summary>
    /// Resolves a URL and stores the resulting track.
    /// Returns the existing track when the URL is already in the library.
    /// </summary>
    public async Task<LibraryTrack> AddFromUrlAsync(string url, CancellationToken cancellationToken)
    {
        var resolution = await _resolution.ResolveAsync(url, cancellationToken);

        var existing = await _tracks.FindBySourceUrlAsync(resolution.SourceUrl, cancellationToken);
        if (existing is not null)
        {
            return Describe(existing);
        }

        var track = new Track
        {
            SourceUrl = resolution.SourceUrl,
            Provider = resolution.Provider,
            Title = resolution.Title,
            Artist = resolution.Artist,
            DurationSeconds = resolution.Duration?.TotalSeconds,
            ThumbnailUrl = resolution.ThumbnailUrl,
            CacheKey = CacheKeyFactory.FromSourceUrl(resolution.SourceUrl),
        };

        return Describe(await _tracks.AddAsync(track, cancellationToken));
    }

    /// <summary>Downloads a track's audio so it plays instantly and works offline.</summary>
    public async Task DownloadAsync(
        Guid trackId,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var track = await _tracks.GetAsync(trackId, cancellationToken)
            ?? throw new MediaResolutionException("That track is no longer in the library.");

        await _cache.StoreAsync(track.CacheKey, new Uri(track.SourceUrl), progress, cancellationToken);
    }

    /// <summary>Deletes a track and its downloaded audio.</summary>
    public async Task<bool> DeleteAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var track = await _tracks.GetAsync(trackId, cancellationToken);
        if (track is null)
        {
            return false;
        }

        _cache.Remove(track.CacheKey);
        return await _tracks.DeleteAsync(trackId, cancellationToken);
    }

    /// <summary>Removes downloaded audio but keeps the track in the library.</summary>
    public async Task<bool> RemoveDownloadAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var track = await _tracks.GetAsync(trackId, cancellationToken);
        if (track is null)
        {
            return false;
        }

        _cache.Remove(track.CacheKey);
        return true;
    }

    /// <summary>Reorders the library to match the supplied track order.</summary>
    public Task<bool> ReorderAsync(IReadOnlyList<Guid> trackIdsInOrder, CancellationToken cancellationToken) =>
        _tracks.ReorderAsync(trackIdsInOrder, cancellationToken);

    /// <summary>Total bytes of downloaded audio.</summary>
    public long GetDownloadedBytes() => _cache.GetSizeBytes();

    /// <summary>Pairs a track with its current download state.</summary>
    private LibraryTrack Describe(Track track) =>
        new(track, _cache.TryGetPath(track.CacheKey) is not null);
}
