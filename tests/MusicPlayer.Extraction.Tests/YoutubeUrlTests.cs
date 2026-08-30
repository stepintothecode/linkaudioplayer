using MusicPlayer.Extraction;
using Xunit;

namespace MusicPlayer.Extraction.Tests;

public sealed class YoutubeUrlTests
{
    [Theory]
    [InlineData("www.youtube.com")]
    [InlineData("youtube.com")]
    [InlineData("m.youtube.com")]
    [InlineData("music.youtube.com")]
    [InlineData("youtu.be")]
    [InlineData("www.youtube-nocookie.com")]
    public void Recognises_youtube_hosts(string host) =>
        Assert.True(YoutubeUrl.IsYoutubeHost(host));

    [Theory]
    [InlineData("soundcloud.com")]
    [InlineData("example.com")]
    [InlineData("notyoutube.com")]
    [InlineData("youtube.com.evil.net")]
    public void Ignores_other_hosts(string host) =>
        Assert.False(YoutubeUrl.IsYoutubeHost(host));
}
