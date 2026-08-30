using LinkAudioPlayer.Extraction;
using LinkAudioPlayer.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LinkAudioPlayer.Extraction.Tests;

// On Android the caller is a Blazor event handler running on the main thread.
// Network work must not resume there: closing an HTTP response does a blocking
// socket read, and StrictMode turns that into NetworkOnMainThreadException.
public sealed class MediaResolutionServiceThreadingTests
{
    [Fact]
    public async Task Resolving_leaves_the_callers_synchronization_context()
    {
        var resolver = new FakeMediaResolver("fake", priority: 1);
        var service = new MediaResolutionService(
            [resolver],
            new DrmSourcePolicy(),
            NullLogger<MediaResolutionService>.Instance);

        using var uiContext = new SingleThreadSynchronizationContext();

        await uiContext.RunAsync(() =>
            service.ResolveAsync("https://www.youtube.com/watch?v=abc", TestContext.Current.CancellationToken));

        Assert.True(resolver.WasCalled);
        Assert.Null(resolver.CapturedContext);
    }

    [Fact]
    public async Task The_policy_still_refuses_drm_before_any_thread_hop()
    {
        var resolver = new FakeMediaResolver("fake", priority: 1);
        var service = new MediaResolutionService(
            [resolver],
            new DrmSourcePolicy(),
            NullLogger<MediaResolutionService>.Instance);

        using var uiContext = new SingleThreadSynchronizationContext();

        await Assert.ThrowsAsync<Abstractions.MediaResolutionException>(() =>
            uiContext.RunAsync(() =>
                service.ResolveAsync("https://open.spotify.com/track/abc", TestContext.Current.CancellationToken)));

        Assert.False(resolver.WasCalled);
    }
}
