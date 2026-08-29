using MusicPlayer.Abstractions;
using MusicPlayer.Extraction;
using Xunit;

namespace MusicPlayer.Extraction.Tests;

// The DRM refusal is the project's main legal guardrail, so it is covered directly.
public sealed class DrmSourcePolicyTests
{
    private readonly DrmSourcePolicy _policy = new();

    [Theory]
    [InlineData("https://open.spotify.com/track/abc")]
    [InlineData("https://spotify.com/track/abc")]
    [InlineData("https://music.apple.com/us/album/abc")]
    [InlineData("https://www.netflix.com/watch/123")]
    [InlineData("https://tidal.com/browse/track/1")]
    [InlineData("https://www.deezer.com/track/1")]
    public void Blocks_drm_hosts(string url)
    {
        var result = _policy.Evaluate(new Uri(url));

        Assert.Equal(SourceVerdict.BlockedDrm, result.Verdict);
        Assert.False(result.IsAllowed);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    [InlineData("https://soundcloud.com/artist/track")]
    [InlineData("https://bandcamp.com/track/abc")]
    [InlineData("https://example.com/podcast.mp3")]
    public void Allows_non_drm_hosts(string url)
    {
        var result = _policy.Evaluate(new Uri(url));

        Assert.Equal(SourceVerdict.Allowed, result.Verdict);
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void Blocks_subdomains_of_drm_hosts()
    {
        var result = _policy.Evaluate(new Uri("https://anything.spotify.com/track/abc"));

        Assert.Equal(SourceVerdict.BlockedDrm, result.Verdict);
    }

    [Fact]
    public void Does_not_block_hosts_that_merely_end_with_a_blocked_name()
    {
        // "notspotify.com" is a different registrable domain and must not be caught.
        var result = _policy.Evaluate(new Uri("https://notspotify.com/track/abc"));

        Assert.Equal(SourceVerdict.Allowed, result.Verdict);
    }

    [Theory]
    [InlineData("file:///c:/music/song.mp3")]
    [InlineData("ftp://example.com/song.mp3")]
    public void Rejects_non_http_schemes(string url)
    {
        var result = _policy.Evaluate(new Uri(url));

        Assert.Equal(SourceVerdict.Rejected, result.Verdict);
    }

    [Fact]
    public void Refusal_explains_why()
    {
        var result = _policy.Evaluate(new Uri("https://open.spotify.com/track/abc"));

        Assert.Contains("DRM", result.Reason, StringComparison.Ordinal);
    }
}
