using MusicPlayer.Abstractions;
using MusicPlayer.Contracts;
using MusicPlayer.Domain;

namespace MusicPlayer.Api.Mapping;

/// <summary>Converts domain entities into transport contracts.</summary>
public static class DtoMapper
{
    /// <summary>Maps a track, consulting the cache to report readiness.</summary>
    public static TrackDto ToDto(this Track track, IMediaCache cache) => new(
        track.Id,
        track.Title,
        track.Artist,
        track.DurationSeconds,
        track.ThumbnailUrl,
        track.SourceUrl,
        track.Provider,
        cache.TryGetPath(track.CacheKey) is not null);

    /// <summary>Maps a playlist without its contents.</summary>
    public static PlaylistSummaryDto ToSummaryDto(this Playlist playlist, int trackCount) =>
        new(playlist.Id, playlist.Name, playlist.Description, trackCount);

    /// <summary>Maps a playlist together with its ordered contents.</summary>
    public static PlaylistDetailDto ToDetailDto(this Playlist playlist, IMediaCache cache)
    {
        var entries = playlist.Items
            .Where(item => item.Track is not null)
            .OrderBy(item => item.Position)
            .Select(item => new PlaylistEntryDto(item.Id, item.Position, item.Track!.ToDto(cache)))
            .ToList();

        return new PlaylistDetailDto(playlist.Id, playlist.Name, playlist.Description, entries);
    }
}
