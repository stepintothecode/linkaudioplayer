using Microsoft.Extensions.Logging.Abstractions;
using MusicPlayer.Abstractions;
using MusicPlayer.Extraction;
using MusicPlayer.TestSupport;
using Xunit;

namespace MusicPlayer.Extraction.Tests;

public sealed class MediaResolutionServiceTests
{
    [Fact]
    public async Task Refuses_drm_sources_before_any_resolver_runs()
    {
        var resolver = new FakeMediaResolver("fake", priority: 1);
        var service = Build(resolver);

        var error = await Assert.ThrowsAsync<MediaResolutionException>(() =>
            service.ResolveAsync("https://open.spotify.com/track/abc", TestContext.Current.CancellationToken));

        Assert.Equal(SourceVerdict.BlockedDrm, error.Verdict);
        Assert.Null(resolver.ResolvedUri);
    }

    [Fact]
    public async Task Dispatches_to_the_lowest_priority_number_that_accepts_the_url()
    {
        var first = new FakeMediaResolver("first", priority: 1);
        var second = new FakeMediaResolver("second", priority: 2);
        var service = Build(second, first);

        var result = await service.ResolveAsync(
            "https://example.com/song",
            TestContext.Current.CancellationToken);

        Assert.Equal("first", result.Provider);
        Assert.NotNull(first.ResolvedUri);
        Assert.Null(second.ResolvedUri);
    }

    [Fact]
    public async Task Falls_through_to_the_next_resolver_when_the_first_declines()
    {
        var declining = new FakeMediaResolver("declining", priority: 1, canResolve: _ => false);
        var accepting = new FakeMediaResolver("accepting", priority: 2);
        var service = Build(declining, accepting);

        var result = await service.ResolveAsync(
            "https://example.com/song",
            TestContext.Current.CancellationToken);

        Assert.Equal("accepting", result.Provider);
    }

    [Fact]
    public async Task Rejects_input_that_is_not_an_absolute_url()
    {
        var service = Build(new FakeMediaResolver("fake", priority: 1));

        await Assert.ThrowsAsync<MediaResolutionException>(() =>
            service.ResolveAsync("not a url", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Fails_clearly_when_no_resolver_accepts_the_url()
    {
        var service = Build(new FakeMediaResolver("fake", priority: 1, canResolve: _ => false));

        var error = await Assert.ThrowsAsync<MediaResolutionException>(() =>
            service.ResolveAsync("https://example.com/song", TestContext.Current.CancellationToken));

        Assert.Contains("No resolver", error.Message, StringComparison.Ordinal);
    }

    private static MediaResolutionService Build(params IMediaResolver[] resolvers) =>
        new(resolvers, new DrmSourcePolicy(), NullLogger<MediaResolutionService>.Instance);
}
