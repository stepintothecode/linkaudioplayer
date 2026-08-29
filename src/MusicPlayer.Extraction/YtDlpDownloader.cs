using Microsoft.Extensions.Options;
using MusicPlayer.Abstractions;

namespace MusicPlayer.Extraction;

/// <summary>Downloads audio with yt-dlp, preferring an audio only format to keep files small.</summary>
public sealed class YtDlpDownloader : IMediaDownloader
{
    private readonly YtDlpProcessRunner _runner;
    private readonly YtDlpOptions _options;

    /// <summary>Creates a downloader backed by the yt-dlp subprocess.</summary>
    public YtDlpDownloader(YtDlpProcessRunner runner, IOptions<YtDlpOptions> options)
    {
        _runner = runner;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<string> DownloadAsync(
        Uri source,
        string destinationDirectory,
        string fileNameStem,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileNameStem);

        Directory.CreateDirectory(destinationDirectory);
        var outputTemplate = Path.Combine(destinationDirectory, fileNameStem + ".%(ext)s");

        string[] arguments =
        [
            "--format", _options.AudioFormat,
            "--output", outputTemplate,
            "--no-playlist",
            "--no-warnings",
            "--no-part",
            source.ToString(),
        ];

        var result = await _runner.RunAsync(
            arguments,
            TimeSpan.FromSeconds(_options.DownloadTimeoutSeconds),
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new MediaResolutionException($"Download failed for '{source}'. {result.StandardError.Trim()}");
        }

        return FindWrittenFile(destinationDirectory, fileNameStem, source);
    }

    /// <summary>Locates the file yt-dlp wrote, whose extension depends on the chosen format.</summary>
    private static string FindWrittenFile(string directory, string fileNameStem, Uri source)
    {
        var match = Directory
            .EnumerateFiles(directory, fileNameStem + ".*")
            .FirstOrDefault();

        return match ?? throw new MediaResolutionException(
            $"yt-dlp reported success but wrote no file for '{source}'.");
    }
}
