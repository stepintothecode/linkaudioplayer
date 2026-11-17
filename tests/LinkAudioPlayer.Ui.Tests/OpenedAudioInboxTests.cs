using LinkAudioPlayer.Ui.Services;
using Xunit;

namespace LinkAudioPlayer.Ui.Tests;

// A file opened from another app must play once, not again on every later visit.
public sealed class OpenedAudioInboxTests
{
    [Fact]
    public void An_opened_file_is_announced_and_taken_once()
    {
        var inbox = new OpenedAudioInbox();
        var arrived = 0;
        inbox.Arrived += () => arrived++;

        inbox.Deliver(new OpenedAudio("content://media/external/audio/media/7", "Yellow"));

        Assert.Equal(1, arrived);
        Assert.Equal("Yellow", inbox.Take()?.Title);
        Assert.Null(inbox.Take());
    }

    [Fact]
    public void A_newer_file_replaces_one_not_yet_played()
    {
        var inbox = new OpenedAudioInbox();

        inbox.Deliver(new OpenedAudio("content://a", "First"));
        inbox.Deliver(new OpenedAudio("content://b", "Second"));

        Assert.Equal("Second", inbox.Take()?.Title);
    }
}
