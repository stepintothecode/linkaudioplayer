using LinkAudioPlayer.Abstractions;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.Streams;

namespace LinkAudioPlayer.Extraction;

/// <summary>Downloads the best audio only stream for a YouTube video.</summary>
public sealed class YoutubeDownloader : IMediaDownloader
{
    private readonly YoutubeClient _youtube;

    /// <summary>Creates the downloader over a shared YoutubeExplode client.</summary>
    public YoutubeDownloader(YoutubeClient youtube) => _youtube = youtube;

    /// <inheritdoc />
    public string Name => "youtube";

    /// <inheritdoc />
    public int Priority => 20;

    /// <inheritdoc />
    public bool CanDownload(Uri source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return YoutubeUrl.IsYoutubeHost(source.Host);
    }

    /// <inheritdoc />
    public async Task<string> DownloadAsync(
        Uri source,
        string destinationDirectory,
        string fileNameStem,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileNameStem);

        Directory.CreateDirectory(destinationDirectory);

        try
        {
            var manifest = await _youtube.Videos.Streams.GetManifestAsync(source.ToString(), cancellationToken);

            // Audio only keeps the file small and matches what the player needs.
            var stream = manifest.GetAudioOnlyStreams().TryGetWithHighestBitrate()
                ?? throw new MediaResolutionException($"'{source}' has no audio only stream.");

            var path = Path.Combine(destinationDirectory, $"{fileNameStem}.{stream.Container.Name}");
            var partial = path + ".part";

            await _youtube.Videos.Streams.DownloadAsync(stream, partial, progress, cancellationToken);

            // Renamed only once complete, so a cancelled download never looks cached.
            File.Move(partial, path, overwrite: true);
            return path;
        }
        catch (YoutubeExplodeException ex)
        {
            throw new MediaResolutionException($"Download failed for '{source}'. {ex.Message}", SourceVerdict.Rejected, ex);
        }
    }
}
