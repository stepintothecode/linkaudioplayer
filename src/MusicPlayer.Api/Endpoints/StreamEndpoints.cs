using MusicPlayer.Abstractions;
using MusicPlayer.Streaming;

namespace MusicPlayer.Api.Endpoints;

/// <summary>
/// Serves cached audio as an ordinary file response.
/// Range processing is enabled so the browser can seek, which is also what lets the
/// Android lock screen scrubber work. The client sees a plain audio file and nothing else.
/// </summary>
public static class StreamEndpoints
{
    /// <summary>Registers the audio streaming route.</summary>
    public static IEndpointRouteBuilder MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        // HEAD is handled alongside GET because media clients often probe with it first.
        // Without it the request falls through to the SPA fallback and returns index.html.
        app.MapMethods("/api/stream/{id:guid}", ["GET", "HEAD"], async (
            Guid id,
            ITrackStore store,
            IMediaCache cache,
            CancellationToken ct) =>
        {
            var track = await store.GetAsync(id, ct);
            if (track is null)
            {
                return Results.NotFound();
            }

            // Fetch on first play. Later requests are served straight from disk.
            var path = cache.TryGetPath(track.CacheKey)
                       ?? await cache.StoreAsync(track.CacheKey, new Uri(track.SourceUrl), ct);

            return Results.File(
                path,
                contentType: ContentTypes.ForPath(path),
                enableRangeProcessing: true);
        })
        .WithTags("Stream")
        .WithSummary("Streams a track's audio, downloading it first if it is not cached yet.");

        return app;
    }
}
