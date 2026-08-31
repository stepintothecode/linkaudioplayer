using System.Web;

namespace LinkAudioPlayer.Extraction;

/// <summary>Recognises the shapes of link YouTube hands out.</summary>
public static class YoutubeUrl
{
    private static readonly string[] Hosts = ["youtube.com", "youtu.be", "youtube-nocookie.com"];

    /// <summary>True when the host is YouTube or one of its subdomains.</summary>
    public static bool IsYoutubeHost(string host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return Hosts.Any(known =>
            host.Equals(known, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith('.' + known, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// True when the URL names a playlist rather than one video.
    /// A link carrying both a video and a list is treated as the video, because that is
    /// what sharing a single song from inside a playlist produces.
    /// </summary>
    public static bool IsPlaylist(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return IsYoutubeHost(uri.Host)
            && PlaylistId(uri) is not null
            && string.IsNullOrEmpty(Query(uri).Get("v"));
    }

    /// <summary>Returns the list identifier, or null when the URL carries none.</summary>
    public static string? PlaylistId(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var list = Query(uri).Get("list");
        return string.IsNullOrWhiteSpace(list) ? null : list;
    }

    /// <summary>
    /// True for an auto generated mix, which YouTube builds per viewer.
    /// These are not real playlists and cannot be listed through the public endpoints.
    /// </summary>
    public static bool IsMix(string playlistId)
    {
        ArgumentNullException.ThrowIfNull(playlistId);

        return playlistId.StartsWith("RD", StringComparison.Ordinal)
            || playlistId.StartsWith("UL", StringComparison.Ordinal)
            || playlistId.StartsWith("TLPQ", StringComparison.Ordinal);
    }

    private static System.Collections.Specialized.NameValueCollection Query(Uri uri) =>
        HttpUtility.ParseQueryString(uri.Query);
}
