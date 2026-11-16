using LinkAudioPlayer.Data;
using LinkAudioPlayer.Library;
using LinkAudioPlayer.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LinkAudioPlayer.Library.Tests;

// Opening the playlists tab often must not rescan the phone every time.
public sealed class DeviceMusicServiceTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;
    private readonly FakeDeviceMusicSource _source = new();
    private readonly ManualClock _clock = new();
    private DeviceMusicService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        _service = new DeviceMusicService(
            _source,
            new DeviceFolderStore(_database),
            NullLogger<DeviceMusicService>.Instance,
            _clock);
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task A_second_refresh_soon_after_the_first_does_not_scan_again()
    {
        await _service.RefreshAsync();
        _clock.Now += TimeSpan.FromSeconds(5);
        await _service.RefreshAsync();

        Assert.Equal(1, _source.Scans);
    }

    [Fact]
    public async Task Scans_again_once_the_last_scan_is_stale_or_when_forced()
    {
        await _service.RefreshAsync();
        _clock.Now += TimeSpan.FromMinutes(1);
        await _service.RefreshAsync();
        await _service.RefreshAsync(force: true);

        Assert.Equal(3, _source.Scans);
    }

    [Fact]
    public async Task Callers_during_a_scan_share_it()
    {
        _source.ScanGate = new TaskCompletionSource();

        var first = _service.RefreshAsync(force: true);
        var second = _service.RefreshAsync(force: true);
        _source.ScanGate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, _source.Scans);
    }

    [Fact]
    public async Task Announces_a_change_only_when_the_scan_found_one()
    {
        var changes = 0;
        _service.Changed += () => changes++;
        _source.Songs.Add(new("content://media/1", "Yellow", null, null, "Music"));

        await _service.RefreshAsync(force: true);
        await _service.RefreshAsync(force: true);

        Assert.Equal(1, changes);
    }

    // Used to show them: Android flags old Samsung recordings as music when they sit
    // outside the standard Recordings folder.
    [Theory]
    [InlineData("VoiceRecorder")]
    [InlineData("Music/Voice Recorder")]
    [InlineData("Recordings/Call")]
    [InlineData("Music/Ringtones/Using")]
    [InlineData("Android/media/com.whatsapp/WhatsApp/Media/WhatsApp Voice Notes/202611")]
    public async Task Leaves_out_recordings_and_phone_sounds(string folder)
    {
        _source.Songs.Add(new("content://media/1", "Memo", null, null, folder));
        _source.Songs.Add(new("content://media/2", "Yellow", null, null, "Music/Coldplay"));

        await _service.RefreshAsync(force: true);

        var folders = await new DeviceFolderStore(_database).ListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["Coldplay"], folders.Select(f => f.Folder.Name));
    }

    [Fact]
    public async Task Does_nothing_without_access()
    {
        _source.HasAccess = false;

        await _service.RefreshAsync(force: true);

        Assert.Equal(0, _source.Scans);
    }
}
