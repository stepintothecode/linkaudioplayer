using Microsoft.Extensions.Logging;
using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.Extraction;

/// <summary>
/// Single entry point for turning a URL into playable metadata.
/// Applies the source policy first, then dispatches to the highest priority resolver
/// that accepts the URL.
/// </summary>
public sealed class MediaResolutionService
{
    private readonly IReadOnlyList<IMediaResolver> _resolvers;
    private readonly ISourcePolicy _policy;
    private readonly ILogger<MediaResolutionService> _logger;

    /// <summary>Creates the service over the registered resolvers, ordered by priority.</summary>
    public MediaResolutionService(
        IEnumerable<IMediaResolver> resolvers,
        ISourcePolicy policy,
        ILogger<MediaResolutionService> logger)
    {
        _resolvers = [.. resolvers.OrderBy(r => r.Priority)];
        _policy = policy;
        _logger = logger;
    }

    /// <summary>Resolves a URL, throwing <see cref="MediaResolutionException"/> when refused or unsupported.</summary>
    /// <param name="url">Raw URL as entered by the user.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    public async Task<MediaResolution> ResolveAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new MediaResolutionException($"'{url}' is not a valid absolute URL.");
        }

        var verdict = _policy.Evaluate(uri);
        if (!verdict.IsAllowed)
        {
            _logger.LogInformation("Refused {Host}: {Reason}", uri.Host, verdict.Reason);
            throw new MediaResolutionException(verdict.Reason, verdict.Verdict);
        }

        var resolver = _resolvers.FirstOrDefault(r => r.CanResolve(uri))
            ?? throw new MediaResolutionException($"No resolver handles '{uri}'.");

        _logger.LogDebug("Resolving {Url} with {Resolver}", uri, resolver.Name);
        return await resolver.ResolveAsync(uri, cancellationToken);
    }
}
