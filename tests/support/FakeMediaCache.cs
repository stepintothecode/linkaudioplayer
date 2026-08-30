using MusicPlayer.Abstractions;

namespace MusicPlayer.TestSupport;

// In-memory stand-in for the on-disk cache. Nothing is cached until a test says so,
// or until StoreAsync is allowed to "download" it.
public sealed class FakeMediaCache : IMediaCache
{
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);

    /// Cache keys passed to StoreAsync, in order.
    public List<string> Downloaded { get; } = [];

    /// When set, StoreAsync throws this instead of succeeding.
    public Exception? StoreFailure { get; set; }

    /// Blocks StoreAsync until the test releases it, for cancellation tests.
    public TaskCompletionSource? StoreGate { get; set; }

    /// Marks a key as already cached.
    public void Add(string cacheKey) => _paths[cacheKey] = $"/cache/{cacheKey}.webm";

    public string? TryGetPath(string cacheKey) =>
        _paths.TryGetValue(cacheKey, out var path) ? path : null;

    public async Task<string> StoreAsync(
        string cacheKey,
        Uri source,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        Downloaded.Add(cacheKey);

        if (StoreGate is not null)
        {
            await StoreGate.Task.WaitAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (StoreFailure is not null)
        {
            throw StoreFailure;
        }

        progress?.Report(1);
        Add(cacheKey);
        return _paths[cacheKey];
    }

    public void Remove(string cacheKey) => _paths.Remove(cacheKey);

    public long GetSizeBytes() => _paths.Count;
}
