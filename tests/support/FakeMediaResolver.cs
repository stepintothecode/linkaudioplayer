using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.TestSupport;

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

    /// The synchronization context ResolveAsync actually ran on.
    /// Null means the work left the caller's context, which is what the app needs on
    /// Android: network work on the main thread throws NetworkOnMainThreadException.
    public SynchronizationContext? CapturedContext { get; private set; }

    /// True when ResolveAsync has run at least once.
    public bool WasCalled { get; private set; }

    public bool CanResolve(Uri uri) => _canResolve(uri);

    public Task<MediaResolution> ResolveAsync(Uri uri, CancellationToken cancellationToken)
    {
        ResolvedUri = uri;
        CapturedContext = SynchronizationContext.Current;
        WasCalled = true;

        return Task.FromResult(new MediaResolution(
            SourceUrl: uri.ToString(),
            Provider: Name,
            Title: "Fake",
            Artist: null,
            Duration: null,
            ThumbnailUrl: null));
    }
}
