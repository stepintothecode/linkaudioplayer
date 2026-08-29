using MusicPlayer.Abstractions;
using MusicPlayer.Domain;
using MusicPlayer.Extraction;

namespace MusicPlayer.Api.Services;

/// <summary>Adds tracks to the library by resolving a URL, reusing any existing entry.</summary>
public sealed class TrackLibraryService
{
    private readonly MediaResolutionService _resolution;
    private readonly ITrackStore _tracks;

    /// <summary>Creates the service over the resolver and track store.</summary>
    public TrackLibraryService(MediaResolutionService resolution, ITrackStore tracks)
    {
        _resolution = resolution;
        _tracks = tracks;
    }

    /// <summary>
    /// Resolves a URL and stores the resulting track.
    /// Returns the existing track when the URL is already in the library.
    /// </summary>
    /// <param name="url">Source URL supplied by the user.</param>
    /// <param name="cancellationToken">Cancels resolution.</param>
    public async Task<Track> AddFromUrlAsync(string url, CancellationToken cancellationToken)
    {
        var resolution = await _resolution.ResolveAsync(url, cancellationToken);

        var existing = await _tracks.FindBySourceUrlAsync(resolution.SourceUrl, cancellationToken);
        if (existing is not null)
        {
            return existing;
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

        return await _tracks.AddAsync(track, cancellationToken);
    }
}
