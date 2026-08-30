using LinkAudioPlayer.Abstractions;
using Microsoft.Extensions.Logging;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.Streams;

namespace LinkAudioPlayer.Extraction;

/// <summary>Downloads the best audio only stream for a YouTube video.</summary>
public sealed class YoutubeDownloader : IMediaDownloader
{
    private readonly YoutubeClient _youtube;
    private readonly ILogger<YoutubeDownloader> _logger;

    /// <summary>Creates the downloader over a shared YoutubeExplode client.</summary>
    public YoutubeDownloader(YoutubeClient youtube, ILogger<YoutubeDownloader> logger)
    {
        _youtube = youtube;
        _logger = logger;
    }

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
            var stream = ChooseAudioStream(manifest, source);

            _logger.LogInformation(
                "Chose {Container} at {Bitrate} for {Url}",
                stream.Container.Name,
                stream.Bitrate,
                source);

            var path = Path.Combine(destinationDirectory, $"{fileNameStem}.{ExtensionFor(stream.Container)}");
            var partial = path + PartialSuffix;

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

    /// <summary>Suffix used while a download is in flight. Shared with the cache, which skips these.</summary>
    internal const string PartialSuffix = ".part";

    /// <summary>
    /// Prefers an mp4 container, which carries AAC.
    /// The highest bitrate audio stream on YouTube is normally WebM Opus, and Android's
    /// MediaPlayer decodes that inconsistently across versions, so a slightly smaller
    /// AAC stream that reliably plays is the better trade.
    /// </summary>
    private static IStreamInfo ChooseAudioStream(StreamManifest manifest, Uri source)
    {
        var audio = manifest.GetAudioOnlyStreams().ToList();

        if (audio.Count == 0)
        {
            throw new MediaResolutionException($"'{source}' has no audio only stream.");
        }

        var mp4 = audio
            .Where(s => s.Container == Container.Mp4)
            .OrderByDescending(s => s.Bitrate)
            .FirstOrDefault();

        return mp4 ?? audio.OrderByDescending(s => s.Bitrate).First();
    }

    /// <summary>Maps a container to the extension Android's extractor expects.</summary>
    private static string ExtensionFor(Container container) =>
        container == Container.Mp4 ? "m4a" : container.Name;
}
