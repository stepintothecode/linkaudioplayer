using MusicPlayer.Abstractions;
using MusicPlayer.Api.Mapping;
using MusicPlayer.Contracts;

namespace MusicPlayer.Api.Endpoints;

/// <summary>Endpoints for creating and editing playlists.</summary>
public static class PlaylistEndpoints
{
    /// <summary>Registers the playlist routes.</summary>
    public static IEndpointRouteBuilder MapPlaylistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/playlists").WithTags("Playlists");

        group.MapGet("/", async (IPlaylistStore store, CancellationToken ct) =>
        {
            var playlists = await store.ListAsync(ct);
            var summaries = new List<PlaylistSummaryDto>(playlists.Count);

            foreach (var playlist in playlists)
            {
                var detail = await store.GetAsync(playlist.Id, ct);
                summaries.Add(playlist.ToSummaryDto(detail?.Items.Count ?? 0));
            }

            return Results.Ok(summaries);
        })
        .WithSummary("Lists all playlists with their track counts.");

        group.MapGet("/{id:guid}", async (
            Guid id,
            IPlaylistStore store,
            IMediaCache cache,
            CancellationToken ct) =>
        {
            var playlist = await store.GetAsync(id, ct);
            return playlist is null
                ? Results.NotFound()
                : Results.Ok(playlist.ToDetailDto(cache));
        })
        .WithSummary("Returns one playlist with its tracks in play order.");

        group.MapPost("/", async (
            CreatePlaylistRequest request,
            IPlaylistStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { error = "Name is required." });
            }

            var playlist = await store.CreateAsync(request.Name.Trim(), request.Description, ct);
            return Results.Created($"/api/playlists/{playlist.Id}", playlist.ToSummaryDto(0));
        })
        .WithSummary("Creates an empty playlist.");

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdatePlaylistRequest request,
            IPlaylistStore store,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { error = "Name is required." });
            }

            var updated = await store.RenameAsync(id, request.Name.Trim(), request.Description, ct);
            return updated ? Results.NoContent() : Results.NotFound();
        })
        .WithSummary("Renames a playlist or changes its description.");

        group.MapDelete("/{id:guid}", async (Guid id, IPlaylistStore store, CancellationToken ct) =>
            await store.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Deletes a playlist. Tracks stay in the library.");

        group.MapPost("/{id:guid}/items", async (
            Guid id,
            AddPlaylistTrackRequest request,
            IPlaylistStore store,
            CancellationToken ct) =>
            await store.AddTrackAsync(id, request.TrackId, ct)
                ? Results.NoContent()
                : Results.NotFound(new { error = "Playlist or track not found." }))
        .WithSummary("Appends a library track to the end of a playlist.");

        group.MapDelete("/{id:guid}/items/{itemId:guid}", async (
            Guid id,
            Guid itemId,
            IPlaylistStore store,
            CancellationToken ct) =>
            await store.RemoveItemAsync(id, itemId, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Removes one entry from a playlist and closes the gap.");

        group.MapPut("/{id:guid}/order", async (
            Guid id,
            ReorderPlaylistRequest request,
            IPlaylistStore store,
            CancellationToken ct) =>
            await store.ReorderAsync(id, request.ItemIds, ct)
                ? Results.NoContent()
                : Results.BadRequest(new { error = "Playlist not found, or the item list does not match it." }))
        .WithSummary("Reorders a playlist to match the supplied entry order.");

        return app;
    }
}
