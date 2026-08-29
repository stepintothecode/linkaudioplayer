using MusicPlayer.Abstractions;
using MusicPlayer.Api.Mapping;
using MusicPlayer.Api.Services;
using MusicPlayer.Contracts;

namespace MusicPlayer.Api.Endpoints;

/// <summary>Library endpoints for listing, adding, caching and removing tracks.</summary>
public static class TrackEndpoints
{
    /// <summary>Registers the track routes.</summary>
    public static IEndpointRouteBuilder MapTrackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tracks").WithTags("Tracks");

        group.MapGet("/", async (ITrackStore store, IMediaCache cache, CancellationToken ct) =>
        {
            var tracks = await store.ListAsync(ct);
            return Results.Ok(tracks.Select(t => t.ToDto(cache)));
        })
        .WithSummary("Lists every track in the library, newest first.");

        group.MapPost("/", async (
            AddTrackRequest request,
            TrackLibraryService library,
            IMediaCache cache,
            CancellationToken ct) =>
        {
            var track = await library.AddFromUrlAsync(request.Url, ct);
            return Results.Created($"/api/tracks/{track.Id}", track.ToDto(cache));
        })
        .WithSummary("Resolves a URL and adds the resulting track to the library.");

        group.MapPost("/{id:guid}/cache", async (
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

            await cache.StoreAsync(track.CacheKey, new Uri(track.SourceUrl), ct);
            return Results.Ok(track.ToDto(cache));
        })
        .WithSummary("Downloads a track to disk so it plays instantly and works offline.");

        group.MapDelete("/{id:guid}/cache", async (
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

            cache.Remove(track.CacheKey);
            return Results.NoContent();
        })
        .WithSummary("Removes the cached audio for a track but keeps it in the library.");

        group.MapDelete("/{id:guid}", async (
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

            cache.Remove(track.CacheKey);
            await store.DeleteAsync(id, ct);
            return Results.NoContent();
        })
        .WithSummary("Deletes a track from the library and drops its cached audio.");

        return app;
    }
}
