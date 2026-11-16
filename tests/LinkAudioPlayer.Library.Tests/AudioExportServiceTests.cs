using LinkAudioPlayer.Abstractions;
using LinkAudioPlayer.Data;
using LinkAudioPlayer.Domain;
using LinkAudioPlayer.Extraction;
using LinkAudioPlayer.Library;
using LinkAudioPlayer.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LinkAudioPlayer.Library.Tests;

// Saving writes a file the user picked and nothing else: no library entry, no copy
// left in the app, and no empty file left behind when it fails.
public sealed class AudioExportServiceTests : IAsyncLifetime
{
    private const string Url = "https://www.youtube.com/watch?v=abc";

    private SqliteTestDatabase _database = null!;
    private readonly FakeMediaCache _cache = new();
    private readonly FakeAudioFileSaver _saver = new();
    private readonly FakeMp3Encoder _encoder = new();
    private AudioExportService _export = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync(TestContext.Current.CancellationToken);

        var resolution = new MediaResolutionService(
            [new FakeMediaResolver("youtube", 10)],
            new DrmSourcePolicy(),
            NullLogger<MediaResolutionService>.Instance);

        _export = new AudioExportService(
            resolution,
            _cache,
            new TrackStore(_database),
            _saver,
            _encoder,
            NullLogger<AudioExportService>.Instance);
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Cancelling_the_save_dialog_downloads_nothing()
    {
        _saver.Cancel = true;

        var saved = await _export.SaveAsync(Url, AudioFormat.Mp3, null, TestContext.Current.CancellationToken);

        Assert.Null(saved);
        Assert.Empty(_cache.Downloaded);
    }

    [Fact]
    public async Task An_mp3_is_tagged_then_encoded()
    {
        var saved = await _export.SaveAsync(Url, AudioFormat.Mp3, null, TestContext.Current.CancellationToken);

        Assert.Equal("Fake.mp3", saved);
        Assert.Equal("audio/mpeg", _saver.MimeType);
        Assert.Single(_encoder.Encoded);

        var written = _saver.Written!;
        Assert.Equal("ID3"u8.ToArray(), written[..3]);
        Assert.Equal(FakeMp3Encoder.Frames, written[^FakeMp3Encoder.Frames.Length..]);
    }

    [Fact]
    public async Task Refuses_m4a_for_audio_that_is_not_aac_and_removes_the_empty_file()
    {
        // The fake cache stores everything as .webm, which is Opus rather than AAC.
        await Assert.ThrowsAsync<MediaResolutionException>(
            () => _export.SaveAsync(Url, AudioFormat.M4a, null, TestContext.Current.CancellationToken));

        Assert.True(_saver.Deleted);
        Assert.Null(_saver.Written);
    }

    [Fact]
    public async Task A_link_outside_the_library_leaves_no_copy_in_the_app()
    {
        await _export.SaveAsync(Url, AudioFormat.Mp3, null, TestContext.Current.CancellationToken);

        Assert.Empty(_cache.ListKeys());
    }

    [Fact]
    public async Task A_library_track_keeps_its_offline_copy()
    {
        var ct = TestContext.Current.CancellationToken;
        var key = CacheKeyFactory.FromSourceUrl(Url);
        await new TrackStore(_database).AddAsync(
            new Track { SourceUrl = Url, Provider = "youtube", Title = "Fake", CacheKey = key },
            ct);
        _cache.Add(key);

        await _export.SaveAsync(Url, AudioFormat.Mp3, null, ct);

        Assert.Empty(_cache.Downloaded);
        Assert.Contains(key, _cache.ListKeys());
    }
}
