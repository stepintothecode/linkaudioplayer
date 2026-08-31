using LinkAudioPlayer.Extraction;
using Xunit;

namespace LinkAudioPlayer.Extraction.Tests;

public sealed class DirectUrlResolverTests
{
    private readonly DirectUrlResolver _resolver = new();

    [Theory]
    [InlineData("https://example.com/song.mp3")]
    [InlineData("https://example.com/song.M4A")]
    [InlineData("https://example.com/path/to/song.opus")]
    public void Handles_media_file_urls(string url) =>
        Assert.True(_resolver.CanResolve(new Uri(url)));

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    [InlineData("https://example.com/page.html")]
    [InlineData("https://example.com/no-extension")]
    public void Ignores_non_media_urls(string url) =>
        Assert.False(_resolver.CanResolve(new Uri(url)));

    [Fact]
    public async Task Derives_a_readable_title_from_the_file_name()
    {
        var result = Assert.Single(await _resolver.ResolveAsync(
            new Uri("https://example.com/media/my_favourite_song.mp3"),
            TestContext.Current.CancellationToken));

        Assert.Equal("my favourite song", result.Title);
        Assert.Equal("direct", result.Provider);
    }

    [Fact]
    public async Task Decodes_percent_encoded_file_names()
    {
        var result = Assert.Single(await _resolver.ResolveAsync(
            new Uri("https://example.com/Hello%20World.mp3"),
            TestContext.Current.CancellationToken));

        Assert.Equal("Hello World", result.Title);
    }

    [Fact]
    public void Runs_before_the_yt_dlp_fallback() =>
        Assert.True(_resolver.Priority < 100);
}
