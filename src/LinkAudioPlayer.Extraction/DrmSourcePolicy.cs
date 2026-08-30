using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.Extraction;

/// <summary>
/// Blocks hosts that serve DRM protected media.
/// Extracting audio from these services requires breaking an access control, which is a
/// circumvention offence under DMCA section 1201 and equivalents. That is a different and
/// far more serious category than a terms of service breach, so it is refused in code
/// rather than left to the operator. See docs/legal-boundaries.md.
/// </summary>
public sealed class DrmSourcePolicy : ISourcePolicy
{
    /// <summary>Hosts refused outright. Matched exactly or as a parent domain.</summary>
    private static readonly string[] DrmHosts =
    [
        "spotify.com",
        "music.apple.com",
        "tv.apple.com",
        "music.amazon.com",
        "netflix.com",
        "primevideo.com",
        "disneyplus.com",
        "hulu.com",
        "max.com",
        "hbomax.com",
        "peacocktv.com",
        "paramountplus.com",
        "tidal.com",
        "deezer.com",
        "audible.com",
        "crunchyroll.com",
    ];

    /// <inheritdoc />
    public SourcePolicyResult Evaluate(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return new SourcePolicyResult(
                SourceVerdict.Rejected,
                $"Unsupported scheme '{uri.Scheme}'. Only http and https are accepted.");
        }

        if (IsDrmHost(uri.Host))
        {
            return new SourcePolicyResult(
                SourceVerdict.BlockedDrm,
                $"'{uri.Host}' serves DRM protected media. This project does not extract audio from " +
                "DRM protected services. Find the track on a non DRM source instead.");
        }

        return SourcePolicyResult.Allow();
    }

    /// <summary>Matches a host against the blocked list, including subdomains.</summary>
    private static bool IsDrmHost(string host) =>
        DrmHosts.Any(blocked =>
            host.Equals(blocked, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith('.' + blocked, StringComparison.OrdinalIgnoreCase));
}
