using LinkAudioPlayer.Abstractions;
using LinkAudioPlayer.Domain;
using LinkAudioPlayer.Extraction;
using Microsoft.Extensions.Logging;

namespace LinkAudioPlayer.Library;

/// <summary>Which file a save produces.</summary>
public enum AudioFormat
{
    /// <summary>YouTube's own AAC audio, saved as it is. Fast and lossless.</summary>
    M4a,

    /// <summary>Re-encoded on the phone. Slower, but plays on anything.</summary>
    Mp3,
}

/// <summary>What a save is doing right now.</summary>
public enum ExportStage
{
    /// <summary>Fetching the audio.</summary>
    Downloading,

    /// <summary>Encoding to MP3.</summary>
    Converting,

    /// <summary>Writing to the chosen location.</summary>
    Saving,
}

/// <summary>Progress of a save.</summary>
/// <param name="Stage">Current step.</param>
/// <param name="Fraction">0 to 1 within that step.</param>
public sealed record ExportProgress(ExportStage Stage, double Fraction);

/// <summary>
/// Saves a link's audio as a file wherever the user picks. Independent of the
/// library: saving a link does not add it, and leaves no copy inside the app.
/// </summary>
public sealed class AudioExportService
{
    private readonly MediaResolutionService _resolution;
    private readonly IMediaCache _cache;
    private readonly ITrackStore _tracks;
    private readonly IAudioFileSaver _saver;
    private readonly IMp3Encoder _encoder;
    private readonly ILogger<AudioExportService> _logger;

    /// <summary>Creates the service.</summary>
    public AudioExportService(
        MediaResolutionService resolution,
        IMediaCache cache,
        ITrackStore tracks,
        IAudioFileSaver saver,
        IMp3Encoder encoder,
        ILogger<AudioExportService> logger)
    {
        _resolution = resolution;
        _cache = cache;
        _tracks = tracks;
        _saver = saver;
        _encoder = encoder;
        _logger = logger;
    }

    /// <summary>
    /// Asks where to save, then downloads and writes the file. Returns the suggested file
    /// name, or null when the user cancelled the save dialog.
    /// </summary>
    public async Task<string?> SaveAsync(
        string url,
        AudioFormat format,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var resolved = await _resolution.ResolveAsync(url.Trim(), cancellationToken);
        if (resolved.Count != 1)
        {
            throw new MediaResolutionException(
                "Saving works on one video at a time. Open a single video and copy its link.");
        }

        var track = resolved[0];
        var fileName = SafeFileName.From(track.Title) + (format == AudioFormat.Mp3 ? ".mp3" : ".m4a");
        var mimeType = format == AudioFormat.Mp3 ? "audio/mpeg" : "audio/mp4";

        var handle = await _saver.PickAsync(fileName, mimeType, cancellationToken);
        if (handle is null)
        {
            return null;
        }

        var cacheKey = CacheKeyFactory.FromSourceUrl(track.SourceUrl);
        string? scratch = null;

        try
        {
            // A track already saved for offline is reused rather than fetched again.
            var source = _cache.TryGetPath(cacheKey) ?? await _cache.StoreAsync(
                cacheKey,
                new Uri(track.SourceUrl),
                Stage(progress, ExportStage.Downloading),
                cancellationToken);

            var upload = source;

            if (format == AudioFormat.M4a && !IsAac(source))
            {
                throw new MediaResolutionException(
                    "This link's audio is not AAC, so it cannot be saved as M4A. Save it as MP3 instead.");
            }

            if (format == AudioFormat.Mp3 && !IsMp3(source))
            {
                scratch = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp3");
                await EncodeAsync(source, scratch, track, progress, cancellationToken);
                upload = scratch;
            }

            progress?.Report(new ExportProgress(ExportStage.Saving, 0));
            await _saver.WriteAsync(handle, upload, cancellationToken);

            _logger.LogInformation("Saved {Url} as {File}", track.SourceUrl, fileName);
            return fileName;
        }
        catch
        {
            // Never leave an empty or half written file where the user chose to save.
            await TryDeleteAsync(handle);
            throw;
        }
        finally
        {
            if (scratch is not null)
            {
                TryDeleteFile(scratch);
            }

            await ForgetUnlessInLibraryAsync(track.SourceUrl, cacheKey);
        }
    }

    /// <summary>Writes the tag first, then the encoder appends the audio after it.</summary>
    private async Task EncodeAsync(
        string source,
        string destination,
        MediaResolution track,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        await using var output = File.Create(destination);
        await output.WriteAsync(Id3Tag.Build(track.Title, track.Artist), cancellationToken);
        await _encoder.EncodeAsync(source, output, Stage(progress, ExportStage.Converting), cancellationToken);
    }

    /// <summary>A link that is not in the library leaves no audio behind in the app.</summary>
    private async Task ForgetUnlessInLibraryAsync(string sourceUrl, string cacheKey)
    {
        try
        {
            if (await _tracks.FindBySourceUrlAsync(sourceUrl, CancellationToken.None) is null)
            {
                _cache.Remove(cacheKey);
            }
        }
        catch (Exception ex)
        {
            // Startup purges orphans anyway, so this is tidiness rather than correctness.
            _logger.LogDebug(ex, "Could not tidy the cache after saving {Url}", sourceUrl);
        }
    }

    private async Task TryDeleteAsync(string handle)
    {
        try
        {
            await _saver.DeleteAsync(handle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove the unfinished saved file");
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Could not delete {Path}", path);
        }
    }

    /// <summary>AAC in an MP4 container. A raw .aac stream would be mislabelled as .m4a.</summary>
    private static bool IsAac(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".m4a" or ".mp4";

    private static bool IsMp3(string path) =>
        Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase);

    private static IProgress<double>? Stage(IProgress<ExportProgress>? progress, ExportStage stage) =>
        progress is null ? null : new InlineProgress(value => progress.Report(new ExportProgress(stage, value)));

    /// <summary>
    /// Forwards straight away. Progress&lt;T&gt; would post to the captured context a second
    /// time, after the caller's own Progress has already done so.
    /// </summary>
    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
