using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.TestSupport;

// Writes a recognisable marker instead of real MP3 frames.
public sealed class FakeMp3Encoder : IMp3Encoder
{
    public static readonly byte[] Frames = [0xFF, 0xFB, 0x90, 0x00];

    /// Input paths it was asked to encode.
    public List<string> Encoded { get; } = [];

    public async Task EncodeAsync(
        string inputPath,
        Stream output,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        Encoded.Add(inputPath);
        await output.WriteAsync(Frames, cancellationToken);
        progress?.Report(1);
    }
}
