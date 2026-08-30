using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.Extraction;

/// <summary>Downloads a URL that already points at a media file.</summary>
public sealed class DirectUrlDownloader : IMediaDownloader
{
    private readonly HttpClient _http;

    /// <summary>Creates the downloader over a shared HTTP client.</summary>
    public DirectUrlDownloader(HttpClient http) => _http = http;

    /// <inheritdoc />
    public string Name => "direct";

    /// <inheritdoc />
    public int Priority => 10;

    /// <inheritdoc />
    public bool CanDownload(Uri source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return MediaExtensions.IsMediaPath(source.AbsolutePath);
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

        var extension = Path.GetExtension(source.AbsolutePath);
        var path = Path.Combine(destinationDirectory, fileNameStem + extension);
        var partial = path + ".part";

        using var response = await _http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new MediaResolutionException(
                $"'{source}' returned status {(int)response.StatusCode}.");
        }

        await CopyToFileAsync(response, partial, progress, cancellationToken);

        // Renamed only once complete, so a cancelled download never looks cached.
        File.Move(partial, path, overwrite: true);
        return path;
    }

    /// <summary>Streams the response to disk, reporting progress when the length is known.</summary>
    private static async Task CopyToFileAsync(
        HttpResponseMessage response,
        string path,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(path);

        var buffer = new byte[81920];
        long written = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            written += read;

            if (total is > 0)
            {
                progress?.Report((double)written / total.Value);
            }
        }
    }
}
