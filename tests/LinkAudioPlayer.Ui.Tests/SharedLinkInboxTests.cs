using LinkAudioPlayer.Ui.Services;
using Xunit;

namespace LinkAudioPlayer.Ui.Tests;

// Apps share a link in whatever text they like, so the link has to be found in it.
public sealed class SharedLinkInboxTests
{
    [Theory]
    [InlineData("https://youtu.be/abc123?si=XyZ", "https://youtu.be/abc123?si=XyZ")]
    [InlineData("Yellow - Coldplay https://youtu.be/abc123", "https://youtu.be/abc123")]
    [InlineData("Watch this: https://www.youtube.com/watch?v=abc123.", "https://www.youtube.com/watch?v=abc123")]
    [InlineData("(https://music.youtube.com/watch?v=abc123)", "https://music.youtube.com/watch?v=abc123")]
    [InlineData("first https://a.example/1 then https://b.example/2", "https://a.example/1")]
    public void Finds_the_link_in_shared_text(string text, string expected) =>
        Assert.Equal(expected, SharedLinkInbox.FindUrl(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("just a message, no link")]
    public void Finds_nothing_when_there_is_no_link(string? text) =>
        Assert.Null(SharedLinkInbox.FindUrl(text));

    [Fact]
    public void A_delivered_link_is_taken_once()
    {
        var inbox = new SharedLinkInbox();
        var arrived = 0;
        inbox.Arrived += () => arrived++;

        Assert.True(inbox.Deliver("Song https://youtu.be/abc123"));

        Assert.Equal(1, arrived);
        Assert.Equal("https://youtu.be/abc123", inbox.Take());
        Assert.Null(inbox.Take());
    }

    [Fact]
    public void Text_without_a_link_is_refused_and_announces_nothing()
    {
        var inbox = new SharedLinkInbox();
        var arrived = 0;
        inbox.Arrived += () => arrived++;

        Assert.False(inbox.Deliver("no link here"));
        Assert.Equal(0, arrived);
    }
}
