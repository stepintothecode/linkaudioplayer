using LinkAudioPlayer.Abstractions;
using Microsoft.Extensions.Logging;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Exceptions;

namespace LinkAudioPlayer.Extraction;

/// <summary>
/// Expands a YouTube playlist link into one track per video.
/// Metadata comes from the listing itself, so adding a playlist costs one request
/// rather than one per track.
/// </summary>
public sealed class YoutubePlaylistResolver : IMediaResolver
{
    /// <summary>Cap on how many videos one link may add, to bound a very long playlist.</summary>
    private const int MaxVideos = 200;

    private readonly YoutubeClient _youtube;
    private readonly ILogger<YoutubePlaylistResolver> _logger;

    /// <summary>Creates the resolver over a shared YoutubeExplode client.</summary>
    public YoutubePlaylistResolver(YoutubeClient youtube, ILogger<YoutubePlaylistResolver> logger)
    {
        _youtube = youtube;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "youtube-playlist";

    /// <summary>Ahead of the single video resolver, which declines list only links.</summary>
    public int Priority => 15;

    /// <inheritdoc />
    public bool CanResolve(Uri uri) => YoutubeUrl.IsPlaylist(uri);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MediaResolution>> ResolveAsync(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var playlistId = YoutubeUrl.PlaylistId(uri)
            ?? throw new MediaResolutionException($"'{uri}' has no playlist id.");

        if (YoutubeUrl.IsMix(playlistId))
        {
            throw new MediaResolutionException(
                "That is a YouTube mix, which YouTube generates per viewer and does not " +
                "publish as a real playlist. Open it, then add the individual videos, or " +
                "use a normal playlist link.");
        }

        try
        {
            var videos = await _youtube.Playlists
                .GetVideosAsync(playlistId, cancellationToken)
                .CollectAsync(MaxVideos);

            _logger.LogInformation("Playlist {Id} produced {Count} videos", playlistId, videos.Count);

            return
            [
                .. videos.Select(video => new MediaResolution(
                    SourceUrl: video.Url,
                    Provider: "youtube",
                    Title: video.Title,
                    Artist: video.Author.ChannelTitle,
                    Duration: video.Duration,
                    ThumbnailUrl: video.Thumbnails.Count > 0
                        ? video.Thumbnails.GetWithHighestResolution().Url
                        : null)),
            ];
        }
        catch (PlaylistUnavailableException ex)
        {
            throw new MediaResolutionException(
                $"That playlist is private or unavailable. {ex.Message}",
                SourceVerdict.Rejected,
                ex);
        }
        catch (YoutubeExplodeException ex)
        {
            throw new MediaResolutionException(
                $"Could not read that playlist. {ex.Message}",
                SourceVerdict.Rejected,
                ex);
        }
    }
}
