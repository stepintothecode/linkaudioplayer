using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.TestSupport;

// Writes a stub file and records the context it ran on.
public sealed class FakeMediaDownloader : IMediaDownloader
{
    private readonly Func<Uri, bool> _canDownload;

    public FakeMediaDownloader(string name, int priority, Func<Uri, bool>? canDownload = null)
    {
        Name = name;
        Priority = priority;
        _canDownload = canDownload ?? (_ => true);
    }

    public string Name { get; }

    public int Priority { get; }

    /// The synchronization context DownloadAsync actually ran on.
    /// Null means the work left the caller's context, which is what the app needs on
    /// Android: network work on the main thread throws NetworkOnMainThreadException.
    public SynchronizationContext? CapturedContext { get; private set; }

    /// True when DownloadAsync has run at least once.
    public bool WasCalled { get; private set; }

    public bool CanDownload(Uri source) => _canDownload(source);

    public async Task<string> DownloadAsync(
        Uri source,
        string destinationDirectory,
        string fileNameStem,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        CapturedContext = SynchronizationContext.Current;
        WasCalled = true;

        Directory.CreateDirectory(destinationDirectory);
        var path = Path.Combine(destinationDirectory, fileNameStem + ".m4a");
        await File.WriteAllBytesAsync(path, [1, 2, 3], cancellationToken);

        progress?.Report(1);
        return path;
    }
}
