using System.Runtime.InteropServices;
using Android.Media;
using LinkAudioPlayer.Abstractions;
using Microsoft.Extensions.Logging;
using Stream = System.IO.Stream;

namespace LinkAudioPlayer.App.Platforms.Android;

/// <summary>
/// Decodes with Android's own codecs and encodes with LAME, the reference MP3 encoder.
/// Android can decode AAC and Opus but has no MP3 encoder, which is why libmp3lame.so
/// ships with the app. See tools/build-lame.ps1.
/// </summary>
public sealed class AndroidMp3Encoder : IMp3Encoder
{
    /// <summary>Constant bitrate. 192 kbps is transparent for YouTube's ~128 kbps AAC source.</summary>
    private const int BitrateKbps = 192;

    /// <summary>LAME's own default: good quality at a speed a phone handles.</summary>
    private const int Quality = 3;

    /// <summary>How long one codec call may wait, in microseconds.</summary>
    private const long WaitUs = 10_000;

    // MediaCodec.dequeueOutputBuffer return codes.
    private const int FormatChanged = -2;

    /// <summary>AudioFormat.ENCODING_PCM_FLOAT, which some decoders choose instead of 16 bit.</summary>
    private const int PcmFloat = 4;

    private readonly ILogger<AndroidMp3Encoder> _logger;

    /// <summary>Creates the encoder.</summary>
    public AndroidMp3Encoder(ILogger<AndroidMp3Encoder> logger) => _logger = logger;

    /// <inheritdoc />
    public Task EncodeAsync(
        string inputPath,
        Stream output,
        IProgress<double>? progress,
        CancellationToken cancellationToken) =>
        // CPU bound for several seconds, so never on the caller's thread.
        Task.Run(() => Encode(inputPath, output, progress, cancellationToken), cancellationToken);

    private void Encode(string inputPath, Stream output, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var extractor = new MediaExtractor();
        extractor.SetDataSource(inputPath);

        var (trackIndex, format) = FindAudioTrack(extractor);
        extractor.SelectTrack(trackIndex);

        var mime = format.GetString(MediaFormat.KeyMime)!;
        var durationUs = format.ContainsKey(MediaFormat.KeyDuration) ? format.GetLong(MediaFormat.KeyDuration) : 0L;
        var sampleRate = format.GetInteger(MediaFormat.KeySampleRate);
        var channels = format.GetInteger(MediaFormat.KeyChannelCount);
        var pcmEncoding = 2; // AudioFormat.ENCODING_PCM_16BIT

        var codec = MediaCodec.CreateDecoderByType(mime);
        codec.Configure(format, null, null, MediaCodecConfigFlags.None);
        codec.Start();

        var info = new MediaCodec.BufferInfo();
        var lame = IntPtr.Zero;
        var inputDone = false;
        var outputDone = false;
        var lastReported = -1;

        byte[] pcm = [];
        short[] left = [];
        short[] right = [];
        byte[] mp3 = [];

        try
        {
            while (!outputDone)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!inputDone)
                {
                    inputDone = FeedInput(codec, extractor);
                }

                var index = codec.DequeueOutputBuffer(info, WaitUs);

                if (index == FormatChanged)
                {
                    var changed = codec.OutputFormat;
                    sampleRate = changed.GetInteger(MediaFormat.KeySampleRate);
                    channels = changed.GetInteger(MediaFormat.KeyChannelCount);
                    pcmEncoding = changed.ContainsKey(MediaFormat.KeyPcmEncoding)
                        ? changed.GetInteger(MediaFormat.KeyPcmEncoding)
                        : 2;
                    continue;
                }

                if (index < 0)
                {
                    continue;
                }

                if (info.Size > 0)
                {
                    if (lame == IntPtr.Zero)
                    {
                        lame = StartLame(sampleRate, channels);
                    }

                    var buffer = codec.GetOutputBuffer(index)!;
                    if (pcm.Length < info.Size)
                    {
                        pcm = new byte[info.Size];
                    }

                    buffer.Position(info.Offset);
                    buffer.Get(pcm, 0, info.Size);

                    var frames = Deinterleave(pcm, info.Size, channels, pcmEncoding, ref left, ref right);
                    var needed = (int)(1.25 * frames) + 7200; // LAME's documented worst case
                    if (mp3.Length < needed)
                    {
                        mp3 = new byte[needed];
                    }

                    var written = Lame.EncodeBuffer(lame, left, right, frames, mp3, mp3.Length);
                    if (written < 0)
                    {
                        throw new InvalidOperationException($"The MP3 encoder failed (code {written}).");
                    }

                    output.Write(mp3, 0, written);
                    Report(progress, info.PresentationTimeUs, durationUs, ref lastReported);
                }

                codec.ReleaseOutputBuffer(index, false);
                outputDone = (info.Flags & MediaCodecBufferFlags.EndOfStream) != 0;
            }

            if (lame != IntPtr.Zero)
            {
                if (mp3.Length < 7200)
                {
                    mp3 = new byte[7200];
                }

                var tail = Lame.EncodeFlush(lame, mp3, mp3.Length);
                if (tail > 0)
                {
                    output.Write(mp3, 0, tail);
                }
            }

            progress?.Report(1);
        }
        finally
        {
            if (lame != IntPtr.Zero)
            {
                Lame.Close(lame);
            }

            try
            {
                codec.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "MediaCodec.Stop failed after encoding");
            }

            codec.Release();
            extractor.Release();
        }
    }

    private static (int Index, MediaFormat Format) FindAudioTrack(MediaExtractor extractor)
    {
        for (var i = 0; i < extractor.TrackCount; i++)
        {
            var format = extractor.GetTrackFormat(i);
            if (format.GetString(MediaFormat.KeyMime)?.StartsWith("audio/", StringComparison.Ordinal) == true)
            {
                return (i, format);
            }
        }

        throw new InvalidDataException("The downloaded file has no audio track.");
    }

    /// <summary>Queues one compressed sample. Returns true once the end of the input was queued.</summary>
    private static bool FeedInput(MediaCodec codec, MediaExtractor extractor)
    {
        var index = codec.DequeueInputBuffer(WaitUs);
        if (index < 0)
        {
            return false;
        }

        var buffer = codec.GetInputBuffer(index)!;
        var size = extractor.ReadSampleData(buffer, 0);

        if (size < 0)
        {
            codec.QueueInputBuffer(index, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
            return true;
        }

        codec.QueueInputBuffer(index, 0, size, extractor.SampleTime, MediaCodecBufferFlags.None);
        extractor.Advance();
        return false;
    }

    /// <summary>
    /// Splits interleaved PCM into left and right 16 bit samples. Mono fills left only,
    /// and anything beyond two channels keeps the front pair. Returns samples per channel.
    /// </summary>
    private static int Deinterleave(
        byte[] pcm,
        int length,
        int channels,
        int encoding,
        ref short[] left,
        ref short[] right)
    {
        var bytesPerSample = encoding == PcmFloat ? 4 : 2;
        var frames = length / (bytesPerSample * channels);

        if (left.Length < frames)
        {
            left = new short[frames];
            right = new short[frames];
        }

        for (var frame = 0; frame < frames; frame++)
        {
            var offset = frame * channels * bytesPerSample;
            left[frame] = ReadSample(pcm, offset, encoding);
            right[frame] = channels > 1 ? ReadSample(pcm, offset + bytesPerSample, encoding) : left[frame];
        }

        return frames;
    }

    private static short ReadSample(byte[] pcm, int offset, int encoding)
    {
        if (encoding != PcmFloat)
        {
            return BitConverter.ToInt16(pcm, offset);
        }

        var value = BitConverter.ToSingle(pcm, offset);
        return (short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue);
    }

    private static IntPtr StartLame(int sampleRate, int channels)
    {
        var lame = Lame.Init();
        if (lame == IntPtr.Zero)
        {
            throw new InvalidOperationException("The MP3 encoder could not start.");
        }

        Lame.SetInSampleRate(lame, sampleRate);
        Lame.SetNumChannels(lame, Math.Min(channels, 2));
        Lame.SetBitrate(lame, BitrateKbps);
        Lame.SetQuality(lame, Quality);

        // The Xing/Info frame needs a seek back to the start, which the save stream may not
        // allow. A constant bitrate file is correct without it.
        Lame.SetWriteVbrTag(lame, 0);

        if (Lame.InitParams(lame) < 0)
        {
            Lame.Close(lame);
            throw new InvalidOperationException("The MP3 encoder rejected the audio format.");
        }

        return lame;
    }

    /// <summary>Reports whole percentages only, so the UI is not redrawn thousands of times.</summary>
    private static void Report(IProgress<double>? progress, long positionUs, long durationUs, ref int lastReported)
    {
        if (progress is null || durationUs <= 0)
        {
            return;
        }

        var percent = (int)Math.Clamp(positionUs * 100 / durationUs, 0, 100);
        if (percent != lastReported)
        {
            lastReported = percent;
            progress.Report(percent / 100.0);
        }
    }

    /// <summary>The few LAME calls needed. Arrays are blittable, so they are pinned, not copied.</summary>
    private static class Lame
    {
        private const string Library = "mp3lame";

        [DllImport(Library, EntryPoint = "lame_init")]
        public static extern IntPtr Init();

        [DllImport(Library, EntryPoint = "lame_set_in_samplerate")]
        public static extern int SetInSampleRate(IntPtr lame, int rate);

        [DllImport(Library, EntryPoint = "lame_set_num_channels")]
        public static extern int SetNumChannels(IntPtr lame, int channels);

        [DllImport(Library, EntryPoint = "lame_set_brate")]
        public static extern int SetBitrate(IntPtr lame, int kbps);

        [DllImport(Library, EntryPoint = "lame_set_quality")]
        public static extern int SetQuality(IntPtr lame, int quality);

        [DllImport(Library, EntryPoint = "lame_set_bWriteVbrTag")]
        public static extern int SetWriteVbrTag(IntPtr lame, int write);

        [DllImport(Library, EntryPoint = "lame_init_params")]
        public static extern int InitParams(IntPtr lame);

        [DllImport(Library, EntryPoint = "lame_encode_buffer")]
        public static extern int EncodeBuffer(
            IntPtr lame,
            short[] left,
            short[] right,
            int samples,
            byte[] mp3,
            int mp3Size);

        [DllImport(Library, EntryPoint = "lame_encode_flush")]
        public static extern int EncodeFlush(IntPtr lame, byte[] mp3, int mp3Size);

        [DllImport(Library, EntryPoint = "lame_close")]
        public static extern int Close(IntPtr lame);
    }
}
