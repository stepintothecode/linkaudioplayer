using LinkAudioPlayer.Abstractions;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Exceptions;

namespace LinkAudioPlayer.Extraction;

/// <summary>Reads metadata for a YouTube link using YoutubeExplode.</summary>
public sealed class YoutubeResolver : IMediaResolver
{
    private readonly YoutubeClient _youtube;

    /// <summary>Creates the resolver over a shared YoutubeExplode client.</summary>
    public YoutubeResolver(YoutubeClient youtube) => _youtube = youtube;

    /// <inheritdoc />
    public string Name => "youtube";

    /// <inheritdoc />
    public int Priority => 20;

    /// <summary>
    /// True for a single YouTube video link. A list only link goes to
    /// <see cref="YoutubePlaylistResolver"/> instead.
    /// </summary>
    public bool CanResolve(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return YoutubeUrl.IsYoutubeHost(uri.Host) && !YoutubeUrl.IsPlaylist(uri);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MediaResolution>> ResolveAsync(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);

        try
        {
            var video = await _youtube.Videos.GetAsync(uri.ToString(), cancellationToken);

            return
            [
                new MediaResolution(
                    SourceUrl: video.Url,
                    Provider: Name,
                    Title: video.Title,
                    Artist: video.Author.ChannelTitle,
                    Duration: video.Duration,
                    ThumbnailUrl: video.Thumbnails.Count > 0
                        ? video.Thumbnails.GetWithHighestResolution().Url
                        : null),
            ];
        }
        // VideoUnavailableException derives from VideoUnplayableException, so it comes first.
        catch (VideoUnavailableException ex)
        {
            throw new MediaResolutionException($"'{uri}' is unavailable or private.", SourceVerdict.Rejected, ex);
        }
        catch (VideoUnplayableException ex)
        {
            throw new MediaResolutionException($"YouTube will not play '{uri}'. {ex.Message}", SourceVerdict.Rejected, ex);
        }
        catch (ArgumentException ex)
        {
            throw new MediaResolutionException(
                $"'{uri}' is not a YouTube video link this app understands.",
                SourceVerdict.Rejected,
                ex);
        }
        catch (YoutubeExplodeException ex)
        {
            throw new MediaResolutionException(
                $"Could not read '{uri}'. YouTube may have changed its site, in which case the app needs updating.",
                SourceVerdict.Rejected,
                ex);
        }
    }
}
