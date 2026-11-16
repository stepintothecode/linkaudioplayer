namespace LinkAudioPlayer.Ui.Services;

/// <summary>
/// Works out which route a swipe lands on, and which way a page should slide in.
/// Kept out of the layout so the edge cases can be tested: the ends of the strip,
/// and a drilled in page such as a playlist, which pops back rather than jumping out.
/// </summary>
public static class TabNavigation
{
    /// <summary>Top level routes, in the order a swipe moves through them.</summary>
    public static readonly string[] Tabs = ["", "playlists", "about"];

    /// <summary>How many tabs there are.</summary>
    public static int Count => Tabs.Length;

    /// <summary>
    /// Position of a route in the strip, or -1 when it belongs to no tab.
    /// A page below a tab reports its parent, so the ends of the strip still resist.
    /// </summary>
    public static int IndexOf(string path)
    {
        var normalised = Normalise(path);

        var exact = Array.IndexOf(Tabs, normalised);
        if (exact >= 0)
        {
            return exact;
        }

        var parent = ParentOf(normalised);
        return parent is null ? -1 : Array.IndexOf(Tabs, parent);
    }

    /// <summary>
    /// Returns the route a swipe should land on, or null when it should do nothing.
    /// Swiping back out of a drilled in page returns to its tab rather than skipping
    /// past it, so the gesture matches the back button already on that screen.
    /// </summary>
    /// <param name="currentPath">Current route, relative and trimmed of slashes.</param>
    /// <param name="direction">1 to move forward through the tabs, -1 to move back.</param>
    public static string? Next(string currentPath, int direction)
    {
        var normalised = Normalise(currentPath);
        var parent = ParentOf(normalised);

        if (parent is not null && direction < 0)
        {
            return parent;
        }

        var index = IndexOf(normalised);
        if (index < 0)
        {
            return null;
        }

        var next = index + direction;
        return next >= 0 && next < Tabs.Length ? Tabs[next] : null;
    }

    /// <summary>
    /// Returns the CSS class for the entry animation, or an empty string for none.
    /// Depth counts, so leaving a playlist slides the same way as moving a tab left.
    /// </summary>
    public static string SlideClass(string fromPath, string toPath)
    {
        var from = Rank(fromPath);
        var to = Rank(toPath);

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

    /// <summary>True when the route is one of the top level tabs.</summary>
    public static bool IsTab(string path) => Array.IndexOf(Tabs, Normalise(path)) >= 0;

    /// <summary>The tab a drilled in route sits under, or null when it is a tab itself.</summary>
    public static string? ParentOf(string normalised)
    {
        if (normalised.Length == 0 || Array.IndexOf(Tabs, normalised) >= 0)
        {
            return null;
        }

        var head = normalised.Split('/')[0];
        return Array.IndexOf(Tabs, head) >= 0 ? head : null;
    }

    /// <summary>
    /// Orders every route on one line, so a slide direction always has an answer.
    /// A tab is a multiple of ten and a page below it sits just after.
    /// </summary>
    private static int Rank(string path)
    {
        var normalised = Normalise(path);
        var index = IndexOf(normalised);

        if (index < 0)
        {
            return -1;
        }

        var isTab = Array.IndexOf(Tabs, normalised) >= 0;
        return (index * 10) + (isTab ? 0 : 1);
    }
}
