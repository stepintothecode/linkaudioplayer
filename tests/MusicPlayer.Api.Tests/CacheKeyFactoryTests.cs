using MusicPlayer.Api.Services;
using Xunit;

namespace MusicPlayer.Api.Tests;

public sealed class CacheKeyFactoryTests
{
    [Fact]
    public void Is_stable_for_the_same_url()
    {
        const string Url = "https://www.youtube.com/watch?v=abc";

        Assert.Equal(CacheKeyFactory.FromSourceUrl(Url), CacheKeyFactory.FromSourceUrl(Url));
    }

    [Fact]
    public void Differs_between_urls()
    {
        var first = CacheKeyFactory.FromSourceUrl("https://example.com/a.mp3");
        var second = CacheKeyFactory.FromSourceUrl("https://example.com/b.mp3");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Produces_a_filesystem_safe_key()
    {
        // Query strings and slashes must not leak into the file name.
        var key = CacheKeyFactory.FromSourceUrl("https://example.com/a b?c=d&e=/f");

        Assert.Matches("^[0-9a-f]{32}$", key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_input(string url) =>
        Assert.Throws<ArgumentException>(() => CacheKeyFactory.FromSourceUrl(url));
}
