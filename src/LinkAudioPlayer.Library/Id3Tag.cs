using System.Text;

namespace LinkAudioPlayer.Library;

/// <summary>
/// Builds an ID3v2.3 tag with a title and an artist, so a saved MP3 shows its name in
/// other music apps rather than "Unknown". Version 2.3 with UTF-16 text is what Android
/// and Windows both read, and UTF-16 keeps non Latin titles intact.
/// </summary>
public static class Id3Tag
{
    /// <summary>The tag bytes, to be written before the first MP3 frame.</summary>
    public static byte[] Build(string title, string? artist)
    {
        ArgumentNullException.ThrowIfNull(title);

        using var frames = new MemoryStream();
        WriteTextFrame(frames, "TIT2", title);

        if (!string.IsNullOrWhiteSpace(artist))
        {
            WriteTextFrame(frames, "TPE1", artist);
        }

        var body = frames.ToArray();
        var tag = new byte[10 + body.Length];

        "ID3"u8.CopyTo(tag);
        tag[3] = 3; // version 2.3
        tag[4] = 0; // revision
        tag[5] = 0; // no flags
        WriteSyncSafe(tag.AsSpan(6, 4), body.Length);
        body.CopyTo(tag, 10);

        return tag;
    }

    /// <summary>Frame id, plain 32 bit size, two flag bytes, then encoding 1 (UTF-16 with BOM).</summary>
    private static void WriteTextFrame(Stream output, string id, string text)
    {
        var payload = new byte[1 + 2 + Encoding.Unicode.GetByteCount(text)];
        payload[0] = 1;
        payload[1] = 0xFF;
        payload[2] = 0xFE;
        Encoding.Unicode.GetBytes(text, payload.AsSpan(3));

        Span<byte> header = stackalloc byte[10];
        Encoding.ASCII.GetBytes(id, header);
        header[4] = (byte)(payload.Length >> 24);
        header[5] = (byte)(payload.Length >> 16);
        header[6] = (byte)(payload.Length >> 8);
        header[7] = (byte)payload.Length;

        output.Write(header);
        output.Write(payload);
    }

    /// <summary>The tag header stores its size seven bits per byte, so it never looks like a frame sync.</summary>
    private static void WriteSyncSafe(Span<byte> target, int value)
    {
        target[0] = (byte)((value >> 21) & 0x7F);
        target[1] = (byte)((value >> 14) & 0x7F);
        target[2] = (byte)((value >> 7) & 0x7F);
        target[3] = (byte)(value & 0x7F);
    }
}
