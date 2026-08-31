using LinkAudioPlayer.Extraction;
using Xunit;

namespace LinkAudioPlayer.Extraction.Tests;

public sealed class YoutubeUrlTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc")]
    [InlineData("https://youtu.be/abc")]
    [InlineData("https://music.youtube.com/watch?v=abc")]
    [InlineData("https://m.youtube.com/watch?v=abc")]
    public void Recognises_youtube_hosts(string url) =>
        Assert.True(YoutubeUrl.IsYoutubeHost(new Uri(url).Host));

    [Theory]
    [InlineData("https://soundcloud.com/a/b")]
    [InlineData("https://notyoutube.com/watch?v=abc")]
    public void Ignores_other_hosts(string url) =>
        Assert.False(YoutubeUrl.IsYoutubeHost(new Uri(url).Host));

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PL123")]
    [InlineData("https://youtube.com/playlist?list=PL123&si=xyz")]
    public void Treats_a_list_only_link_as_a_playlist(string url) =>
        Assert.True(YoutubeUrl.IsPlaylist(new Uri(url)));

    // Sharing one song from inside a playlist produces both parameters. The video
    // is what the user picked, so that wins.
    [Fact]
    public void Treats_a_link_carrying_both_as_a_single_video()
    {
        var uri = new Uri("https://www.youtube.com/watch?v=abc&list=PL123");

        Assert.False(YoutubeUrl.IsPlaylist(uri));
        Assert.Equal("PL123", YoutubeUrl.PlaylistId(uri));
    }

    [Fact]
    public void A_plain_video_link_has_no_playlist_id() =>
        Assert.Null(YoutubeUrl.PlaylistId(new Uri("https://www.youtube.com/watch?v=abc")));

    [Theory]
    [InlineData("RDKyg8J0mt5nY")]
    [InlineData("RDMM")]
    [InlineData("ULabc123")]
    public void Recognises_auto_generated_mixes(string playlistId) =>
        Assert.True(YoutubeUrl.IsMix(playlistId));

    [Theory]
    [InlineData("PLabc123")]
    [InlineData("OLAK5uy_abc")]
    public void Leaves_real_playlists_alone(string playlistId) =>
        Assert.False(YoutubeUrl.IsMix(playlistId));
}
