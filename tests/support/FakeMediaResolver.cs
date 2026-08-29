using MusicPlayer.Abstractions;

namespace MusicPlayer.TestSupport;

// Records what it was asked to resolve so tests can assert on dispatch order.
public sealed class FakeMediaResolver : IMediaResolver
{
    private readonly Func<Uri, bool> _canResolve;

    public FakeMediaResolver(string name, int priority, Func<Uri, bool>? canResolve = null)
    {
        Name = name;
        Priority = priority;
        _canResolve = canResolve ?? (_ => true);
    }

    public string Name { get; }

    public int Priority { get; }

    /// Set when ResolveAsync runs.
    public Uri? ResolvedUri { get; private set; }

    public bool CanResolve(Uri uri) => _canResolve(uri);

    public Task<MediaResolution> ResolveAsync(Uri uri, CancellationToken cancellationToken)
    {
        ResolvedUri = uri;

        return Task.FromResult(new MediaResolution(
            SourceUrl: uri.ToString(),
            Provider: Name,
            Title: "Fake",
            Artist: null,
            Duration: null,
            ThumbnailUrl: null));
    }
}
