using System.Net;
using System.Net.Http.Json;
using MusicPlayer.Contracts;

namespace MusicPlayer.Web.Services;

/// <summary>Typed wrapper over the server API.</summary>
public sealed class MusicPlayerApiClient
{
    private readonly HttpClient _http;

    /// <summary>Creates the client over a configured <see cref="HttpClient"/>.</summary>
    public MusicPlayerApiClient(HttpClient http) => _http = http;

    /// <summary>Lists every track in the library.</summary>
    public async Task<IReadOnlyList<TrackDto>> GetTracksAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<TrackDto>>(ApiRoutes.Tracks, ct) ?? [];

    /// <summary>Adds a track from a URL. Returns the server message on refusal.</summary>
    public async Task<(TrackDto? Track, string? Error)> AddTrackAsync(string url, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Tracks, new AddTrackRequest(url), ct);

        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<TrackDto>(ct), null);
        }

        return (null, await ReadErrorAsync(response, ct));
    }

    /// <summary>Downloads a track to the server cache for offline playback.</summary>
    public async Task<(TrackDto? Track, string? Error)> CacheTrackAsync(Guid trackId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(ApiRoutes.Cache(trackId), content: null, ct);

        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<TrackDto>(ct), null);
        }

        return (null, await ReadErrorAsync(response, ct));
    }

    /// <summary>Deletes a track from the library.</summary>
    public async Task DeleteTrackAsync(Guid trackId, CancellationToken ct = default) =>
        await _http.DeleteAsync($"{ApiRoutes.Tracks}/{trackId}", ct);

    /// <summary>Lists all playlists.</summary>
    public async Task<IReadOnlyList<PlaylistSummaryDto>> GetPlaylistsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<PlaylistSummaryDto>>(ApiRoutes.Playlists, ct) ?? [];

    /// <summary>Returns one playlist with its contents, or null when missing.</summary>
    public async Task<PlaylistDetailDto?> GetPlaylistAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{ApiRoutes.Playlists}/{id}", ct);
        return response.StatusCode == HttpStatusCode.NotFound
            ? null
            : await response.Content.ReadFromJsonAsync<PlaylistDetailDto>(ct);
    }

    /// <summary>Creates an empty playlist.</summary>
    public async Task<PlaylistSummaryDto?> CreatePlaylistAsync(
        string name,
        string? description,
        CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            ApiRoutes.Playlists,
            new CreatePlaylistRequest(name, description),
            ct);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PlaylistSummaryDto>(ct)
            : null;
    }

    /// <summary>Deletes a playlist.</summary>
    public async Task DeletePlaylistAsync(Guid id, CancellationToken ct = default) =>
        await _http.DeleteAsync($"{ApiRoutes.Playlists}/{id}", ct);

    /// <summary>Appends a library track to a playlist.</summary>
    public async Task AddToPlaylistAsync(Guid playlistId, Guid trackId, CancellationToken ct = default) =>
        await _http.PostAsJsonAsync(
            $"{ApiRoutes.Playlists}/{playlistId}/items",
            new AddPlaylistTrackRequest(trackId),
            ct);

    /// <summary>Removes one entry from a playlist.</summary>
    public async Task RemoveFromPlaylistAsync(Guid playlistId, Guid itemId, CancellationToken ct = default) =>
        await _http.DeleteAsync($"{ApiRoutes.Playlists}/{playlistId}/items/{itemId}", ct);

    /// <summary>Reorders a playlist to the supplied entry order.</summary>
    public async Task ReorderPlaylistAsync(
        Guid playlistId,
        IReadOnlyList<Guid> itemIds,
        CancellationToken ct = default) =>
        await _http.PutAsJsonAsync(
            $"{ApiRoutes.Playlists}/{playlistId}/order",
            new ReorderPlaylistRequest(itemIds),
            ct);

    /// <summary>Reads the server error message, falling back to the status code.</summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
            if (!string.IsNullOrWhiteSpace(problem?.Error))
            {
                return problem.Error;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or NotSupportedException or System.Text.Json.JsonException)
        {
            // Body was not the expected shape. Fall through to the status text.
        }

        return $"Request failed with status {(int)response.StatusCode}.";
    }

    /// <summary>Shape of the error body returned by the API.</summary>
    private sealed record ErrorResponse(string? Error, string? Verdict);
}
