using Microsoft.Extensions.Logging;
using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.Storage;

/// <summary>Dispatches a download to the highest priority downloader that accepts the URL.</summary>
public sealed class MediaDownloadService
{
    private readonly IReadOnlyList<IMediaDownloader> _downloaders;
    private readonly ILogger<MediaDownloadService> _logger;

    /// <summary>Creates the service over the registered downloaders, ordered by priority.</summary>
    public MediaDownloadService(
        IEnumerable<IMediaDownloader> downloaders,
        ILogger<MediaDownloadService> logger)
    {
        _downloaders = [.. downloaders.OrderBy(d => d.Priority)];
        _logger = logger;
    }

    /// <summary>Downloads audio and returns the absolute path of the written file.</summary>
    public async Task<string> DownloadAsync(
        Uri source,
        string destinationDirectory,
        string fileNameStem,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var downloader = _downloaders.FirstOrDefault(d => d.CanDownload(source))
            ?? throw new MediaResolutionException($"No downloader handles '{source}'.");

        _logger.LogDebug("Downloading {Url} with {Downloader}", source, downloader.Name);
        return await downloader.DownloadAsync(
            source,
            destinationDirectory,
            fileNameStem,
            progress,
            cancellationToken);
    }
}
