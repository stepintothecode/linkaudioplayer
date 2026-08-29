using System.Text.Json;
using Microsoft.Extensions.Options;
using MusicPlayer.Abstractions;

namespace MusicPlayer.Extraction;

/// <summary>
/// Resolves any URL yt-dlp recognises, which covers roughly 1800 sites.
/// Acts as the fallback resolver, so it is tried last.
/// </summary>
public sealed class YtDlpResolver : IMediaResolver
{
    private readonly YtDlpProcessRunner _runner;
    private readonly YtDlpOptions _options;

    /// <summary>Creates a resolver backed by the yt-dlp subprocess.</summary>
    public YtDlpResolver(YtDlpProcessRunner runner, IOptions<YtDlpOptions> options)
    {
        _runner = runner;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Name => "yt-dlp";

    /// <inheritdoc />
    public int Priority => 100;

    /// <summary>Accepts every http and https URL. Suitability is decided by yt-dlp itself.</summary>
    public bool CanResolve(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

    /// <inheritdoc />
    public async Task<MediaResolution> ResolveAsync(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);

        string[] arguments =
        [
            "--dump-single-json",
            "--no-playlist",
            "--no-warnings",
            "--skip-download",
            uri.ToString(),
        ];

        var result = await _runner.RunAsync(
            arguments,
            TimeSpan.FromSeconds(_options.MetadataTimeoutSeconds),
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new MediaResolutionException(
                $"yt-dlp could not read '{uri}'. {FirstErrorLine(result.StandardError)}");
        }

        var metadata = Deserialize(result.StandardOutput, uri);

        return new MediaResolution(
            SourceUrl: metadata.WebpageUrl ?? uri.ToString(),
            Provider: Name,
            Title: metadata.Track ?? metadata.Title ?? uri.Host,
            Artist: metadata.Artist ?? metadata.Uploader,
            Duration: metadata.Duration is > 0 ? TimeSpan.FromSeconds(metadata.Duration.Value) : null,
            ThumbnailUrl: metadata.Thumbnail);
    }

    /// <summary>Parses the yt-dlp JSON document, converting malformed output into a resolution error.</summary>
    private static YtDlpMetadata Deserialize(string json, Uri uri)
    {
        try
        {
            return JsonSerializer.Deserialize<YtDlpMetadata>(json)
                   ?? throw new MediaResolutionException($"yt-dlp returned no metadata for '{uri}'.");
        }
        catch (JsonException ex)
        {
            throw new MediaResolutionException($"Could not parse yt-dlp output for '{uri}'.", SourceVerdict.Rejected, ex);
        }
    }

    /// <summary>Extracts the first meaningful stderr line for display.</summary>
    private static string FirstErrorLine(string stderr)
    {
        var line = stderr
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(l => l.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase));

        return line ?? "The source may be unavailable or unsupported.";
    }
}
