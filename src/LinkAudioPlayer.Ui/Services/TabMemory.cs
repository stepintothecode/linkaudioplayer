namespace LinkAudioPlayer.Ui.Services;

/// <summary>
/// Remembers where you were inside a tab, so returning to it puts you back rather
/// than at the top. Only the playlists tab has anywhere to be, since it is the only
/// one you can drill into.
/// </summary>
public sealed class TabMemory
{
    /// <summary>Route of the playlist last opened, or null when the list was last seen.</summary>
    public string? PlaylistRoute { get; private set; }

    /// <summary>Records that a playlist is open.</summary>
    public void OpenedPlaylist(Guid playlistId) => PlaylistRoute = $"playlists/{playlistId}";

    /// <summary>
    /// Forgets the open playlist.
    /// Called when the list itself is shown, so backing out then returning gives the
    /// list rather than dropping you into a playlist you just left.
    /// </summary>
    public void ClosedPlaylist() => PlaylistRoute = null;

    /// <summary>
    /// Turns a tab route into the route that should actually be visited.
    /// Anything other than the playlists tab is returned unchanged.
    /// </summary>
    public string Resolve(string tabRoute) =>
        tabRoute == "playlists" && PlaylistRoute is { } remembered ? remembered : tabRoute;
}
