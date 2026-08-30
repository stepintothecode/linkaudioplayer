namespace MusicPlayer.Abstractions;

/// <summary>Stores fetched audio on device so playback and offline use read from local files.</summary>
public interface IMediaCache
{
    /// <summary>Returns the path to cached audio, or null when the track is not cached.</summary>
    string? TryGetPath(string cacheKey);

    /// <summary>Fetches the source into the cache and returns the resulting path.</summary>
    Task<string> StoreAsync(
        string cacheKey,
        Uri source,
        IProgress<double>? progress,
        CancellationToken cancellationToken);

    /// <summary>Deletes cached audio for a track. Safe to call when nothing is cached.</summary>
    void Remove(string cacheKey);

    /// <summary>Total bytes currently held in the cache.</summary>
    long GetSizeBytes();
}
