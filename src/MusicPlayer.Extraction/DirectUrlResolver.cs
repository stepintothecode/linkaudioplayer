using MusicPlayer.Abstractions;

namespace MusicPlayer.Extraction;

/// <summary>
/// Handles URLs that already point at a media file, such as a podcast enclosure or a
/// self hosted mp3.
/// </summary>
public sealed class DirectUrlResolver : IMediaResolver
{
    /// <inheritdoc />
    public string Name => "direct";

    /// <inheritdoc />
    public int Priority => 10;

    /// <summary>True when the URL path ends with a known media extension.</summary>
    public bool CanResolve(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && MediaExtensions.IsMediaPath(uri.AbsolutePath);
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
