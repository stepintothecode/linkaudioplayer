using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicPlayer.Abstractions;

namespace MusicPlayer.Storage;

/// <summary>
/// Stores fetched audio under one directory, a single file per cache key.
/// Concurrent requests for the same key wait on one download rather than racing.
/// </summary>
public sealed class FileSystemMediaCache : IMediaCache
{
    private readonly MediaDownloadService _downloads;
    private readonly ILogger<FileSystemMediaCache> _logger;
    private readonly string _root;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    /// <summary>Creates the cache and ensures its directory exists.</summary>
    public FileSystemMediaCache(
        MediaDownloadService downloads,
        IOptions<MediaCacheOptions> options,
        ILogger<FileSystemMediaCache> logger)
    {
        _downloads = downloads;
        _logger = logger;
        _root = options.Value.Directory;

        Directory.CreateDirectory(_root);
    }

    /// <inheritdoc />
    public string? TryGetPath(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        return Directory.EnumerateFiles(_root, cacheKey + ".*").FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(
        string cacheKey,
        Uri source,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(source);

        var gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            // Another caller may have completed the download while this one waited.
            var existing = TryGetPath(cacheKey);
            if (existing is not null)
            {
                return existing;
            }

            _logger.LogInformation("Caching {Source} as {CacheKey}", source, cacheKey);
            return await _downloads.DownloadAsync(source, _root, cacheKey, progress, cancellationToken);
        }
        finally
        {
            gate.Release();
            _locks.TryRemove(cacheKey, out _);
        }
    }

    /// <inheritdoc />
    public void Remove(string cacheKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        var path = TryGetPath(cacheKey);
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not delete cached file {Path}", path);
        }
    }

    /// <inheritdoc />
    public long GetSizeBytes() =>
        Directory.EnumerateFiles(_root).Sum(path => new FileInfo(path).Length);
}
