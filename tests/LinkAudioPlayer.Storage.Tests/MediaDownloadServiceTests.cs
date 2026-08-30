using LinkAudioPlayer.Storage;
using LinkAudioPlayer.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LinkAudioPlayer.Storage.Tests;

// The download is what actually crashed on device: it ran on the Android main
// thread, and disposing the HTTP response there threw NetworkOnMainThreadException.
public sealed class MediaDownloadServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task Downloading_leaves_the_callers_synchronization_context()
    {
        var downloader = new FakeMediaDownloader("fake", priority: 1);
        var service = Build(downloader);

        using var uiContext = new SingleThreadSynchronizationContext();

        await uiContext.RunAsync(() => service.DownloadAsync(
            new Uri("https://www.youtube.com/watch?v=abc"),
            _directory,
            "key",
            progress: null,
            TestContext.Current.CancellationToken));

        Assert.True(downloader.WasCalled);
        Assert.Null(downloader.CapturedContext);
    }

    [Fact]
    public async Task Dispatches_to_the_lowest_priority_number_that_accepts_the_url()
    {
        var first = new FakeMediaDownloader("first", priority: 1);
        var second = new FakeMediaDownloader("second", priority: 2);
        var service = Build(second, first);

        await service.DownloadAsync(
            new Uri("https://example.com/song.mp3"),
            _directory,
            "key",
            progress: null,
            TestContext.Current.CancellationToken);

        Assert.True(first.WasCalled);
        Assert.False(second.WasCalled);
    }

    [Fact]
    public async Task Fails_clearly_when_nothing_handles_the_url()
    {
        var service = Build(new FakeMediaDownloader("fake", priority: 1, canDownload: _ => false));

        var error = await Assert.ThrowsAsync<Abstractions.MediaResolutionException>(() =>
            service.DownloadAsync(
                new Uri("https://example.com/song.mp3"),
                _directory,
                "key",
                progress: null,
                TestContext.Current.CancellationToken));

        Assert.Contains("No downloader", error.Message, StringComparison.Ordinal);
    }

    private static MediaDownloadService Build(params Abstractions.IMediaDownloader[] downloaders) =>
        new(downloaders, NullLogger<MediaDownloadService>.Instance);
}
