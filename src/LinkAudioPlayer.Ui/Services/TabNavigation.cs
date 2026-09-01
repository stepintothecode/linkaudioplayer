namespace LinkAudioPlayer.Ui.Services;

/// <summary>
/// Works out which tab a swipe lands on, and which way a page should slide in.
/// Kept out of the layout so the edge cases can be tested: the ends of the strip,
/// and sub pages such as a playlist, which must not move at all.
/// </summary>
public static class TabNavigation
{
    /// <summary>Top level routes, in the order a swipe moves through them.</summary>
    public static readonly string[] Tabs = ["", "playlists", "about"];

    /// <summary>
    /// Returns the route a swipe should land on, or null when it should do nothing.
    /// </summary>
    /// <param name="currentPath">Current route, relative and trimmed of slashes.</param>
    /// <param name="direction">1 to move forward through the tabs, -1 to move back.</param>
    public static string? Next(string currentPath, int direction)
    {
        var index = Array.IndexOf(Tabs, Normalise(currentPath));
        if (index < 0)
        {
            return null;
        }

        var next = index + direction;
        return next >= 0 && next < Tabs.Length ? Tabs[next] : null;
    }

    /// <summary>
    /// Returns the CSS class for the entry animation, or an empty string for none.
    /// Only movement between two tabs slides; anything involving a sub page appears.
    /// </summary>
    public static string SlideClass(string fromPath, string toPath)
    {
        var from = Array.IndexOf(Tabs, Normalise(fromPath));
        var to = Array.IndexOf(Tabs, Normalise(toPath));

        if (from < 0 || to < 0 || from == to)
        {
            return string.Empty;
        }

        return to > from ? "from-right" : "from-left";
    }

    /// <summary>Strips a query string and surrounding slashes from a route.</summary>
    public static string Normalise(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var cut = path.IndexOf('?', StringComparison.Ordinal);
        var trimmed = cut >= 0 ? path[..cut] : path;

        return trimmed.Trim('/');
    }
}
