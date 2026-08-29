namespace MusicPlayer.Contracts;

/// <summary>Relative API paths, shared so the client and server cannot drift.</summary>
public static class ApiRoutes
{
    /// <summary>Track collection endpoint.</summary>
    public const string Tracks = "api/tracks";

    /// <summary>Playlist collection endpoint.</summary>
    public const string Playlists = "api/playlists";

    /// <summary>Returns the audio stream path for a track.</summary>
    public static string Stream(Guid trackId) => $"api/stream/{trackId}";

    /// <summary>Returns the cache download trigger path for a track.</summary>
    public static string Cache(Guid trackId) => $"api/tracks/{trackId}/cache";
}
