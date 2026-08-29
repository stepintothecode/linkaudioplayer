using MusicPlayer.Abstractions;

namespace MusicPlayer.Extraction;

/// <summary>
/// Handles URLs that already point at a media file, such as a podcast enclosure or a
/// self hosted mp3. Skips the yt-dlp subprocess entirely.
/// </summary>
public sealed class DirectUrlResolver : IMediaResolver
{
    private static readonly string[] MediaExtensions =
        [".mp3", ".m4a", ".aac", ".ogg", ".oga", ".opus", ".flac", ".wav", ".webm", ".mp4", ".m4b"];

    /// <inheritdoc />
    public string Name => "direct";

    /// <inheritdoc />
    public int Priority => 10;

    /// <summary>True when the URL path ends with a known media extension.</summary>
    public bool CanResolve(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var extension = Path.GetExtension(uri.AbsolutePath);
        return MediaExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Derives a title from the file name. No network call is made.</summary>
    public Task<MediaResolution> ResolveAsync(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var fileName = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
        var title = string.IsNullOrWhiteSpace(fileName)
            ? uri.Host
            : Uri.UnescapeDataString(fileName).Replace('_', ' ').Trim();

        return Task.FromResult(new MediaResolution(
            SourceUrl: uri.ToString(),
            Provider: Name,
            Title: title,
            Artist: uri.Host,
            Duration: null,
            ThumbnailUrl: null));
    }
}
