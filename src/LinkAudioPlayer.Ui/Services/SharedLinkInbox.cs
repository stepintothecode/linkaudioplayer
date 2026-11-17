using System.Text.RegularExpressions;

namespace LinkAudioPlayer.Ui.Services;

/// <summary>
/// Holds a link shared into the app from another app, such as YouTube's Share menu,
/// until the library page picks it up. A singleton, because the share arrives on the
/// platform side, possibly before any page exists.
/// </summary>
public sealed partial class SharedLinkInbox
{
    private readonly Lock _gate = new();
    private string? _pending;

    /// <summary>Raised when a link arrives.</summary>
    public event Action? Arrived;

    /// <summary>
    /// Takes shared text and keeps the link inside it. Returns false when it held none,
    /// such as a plain message shared by mistake.
    /// </summary>
    public bool Deliver(string? sharedText)
    {
        var url = FindUrl(sharedText);
        if (url is null)
        {
            return false;
        }

        lock (_gate)
        {
            _pending = url;
        }

        Arrived?.Invoke();
        return true;
    }

    /// <summary>The waiting link, once. Null when nothing is waiting.</summary>
    public string? Take()
    {
        lock (_gate)
        {
            var url = _pending;
            _pending = null;
            return url;
        }
    }

    /// <summary>
    /// The first web link in shared text. Apps often share a title with the link, as in
    /// "Yellow - Coldplay https://youtu.be/abc?si=x", so the link is picked out of it.
    /// </summary>
    public static string? FindUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = UrlPattern().Match(text);
        return match.Success ? match.Value.TrimEnd('.', ',', ')', ']', '"', '\'', '!', '?') : null;
    }

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();
}
