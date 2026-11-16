namespace LinkAudioPlayer.Abstractions;

/// <summary>Converts a local audio file to MP3. Decoding needs the platform's codecs.</summary>
public interface IMp3Encoder
{
    /// <summary>Decodes the input and appends MP3 frames to the output stream.</summary>
    /// <param name="inputPath">Any audio file the platform can decode, such as m4a or webm.</param>
    /// <param name="output">Stream to append to. Anything already written, such as a tag, stays first.</param>
    /// <param name="progress">Receives values from 0 to 1.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    Task EncodeAsync(
        string inputPath,
        Stream output,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}
