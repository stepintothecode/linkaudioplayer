using System.Text;
using LinkAudioPlayer.Library;
using Xunit;

namespace LinkAudioPlayer.Library.Tests;

// Checked against the ID3v2.3 layout byte by byte, since a malformed tag makes other
// apps skip the title or, worse, the whole file.
public sealed class Id3TagTests
{
    [Fact]
    public void Starts_with_a_v2_3_header_whose_size_covers_the_frames()
    {
        var tag = Id3Tag.Build("Yellow", "Coldplay");

        Assert.Equal("ID3"u8.ToArray(), tag[..3]);
        Assert.Equal(3, tag[3]);
        Assert.Equal(tag.Length - 10, SyncSafe(tag.AsSpan(6, 4)));
    }

    [Fact]
    public void Stores_the_title_as_utf16_so_any_script_survives()
    {
        const string title = "तुम ही हो";

        var tag = Id3Tag.Build(title, artist: null);

        Assert.Equal("TIT2"u8.ToArray(), tag[10..14]);
        var size = (tag[14] << 24) | (tag[15] << 16) | (tag[16] << 8) | tag[17];
        Assert.Equal(1, tag[20]); // UTF-16 with BOM
        Assert.Equal(title, Encoding.Unicode.GetString(tag, 23, size - 3));
    }

    [Fact]
    public void Leaves_out_the_artist_frame_when_there_is_none()
    {
        var tag = Id3Tag.Build("Yellow", "  ");

        Assert.DoesNotContain("TPE1", Encoding.ASCII.GetString(tag));
    }

    private static int SyncSafe(ReadOnlySpan<byte> bytes) =>
        (bytes[0] << 21) | (bytes[1] << 14) | (bytes[2] << 7) | bytes[3];
}
