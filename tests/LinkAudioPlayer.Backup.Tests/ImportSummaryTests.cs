using LinkAudioPlayer.Backup;
using Xunit;

namespace LinkAudioPlayer.Backup.Tests;

// The summary used to read "1 track(s) and 1 playlist entr(ies)".
public sealed class ImportSummaryTests
{
    [Fact]
    public void Uses_the_singular_for_one()
    {
        var summary = new ImportSummary { TracksAdded = 1, PlaylistEntriesAdded = 1 };

        Assert.Equal("Added 1 track and 1 playlist entry", summary.ToString());
    }

    [Fact]
    public void Uses_the_plural_for_more_than_one()
    {
        var summary = new ImportSummary { TracksAdded = 4, PlaylistEntriesAdded = 7 };

        Assert.Equal("Added 4 tracks and 7 playlist entries", summary.ToString());
    }

    [Fact]
    public void Uses_the_plural_for_none()
    {
        var summary = new ImportSummary { TracksAdded = 3, PlaylistEntriesAdded = 0 };

        Assert.Equal("Added 3 tracks and 0 playlist entries", summary.ToString());
    }

    [Fact]
    public void Says_so_when_nothing_changed()
    {
        var summary = new ImportSummary { TracksSkipped = 5 };

        Assert.True(summary.IsEmpty);
        Assert.Equal("Everything in that backup was already here", summary.ToString());
    }

    [Fact]
    public void A_new_playlist_alone_is_not_empty()
    {
        var summary = new ImportSummary { PlaylistsCreated = 1 };

        Assert.False(summary.IsEmpty);
    }
}
