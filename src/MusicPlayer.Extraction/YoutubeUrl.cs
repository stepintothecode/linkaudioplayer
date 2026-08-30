namespace MusicPlayer.Extraction;

/// <summary>Recognises the hosts YouTube serves links from.</summary>
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
}
