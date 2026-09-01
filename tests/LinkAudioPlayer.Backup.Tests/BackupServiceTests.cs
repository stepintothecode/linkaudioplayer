using LinkAudioPlayer.Backup;
using LinkAudioPlayer.Data;
using LinkAudioPlayer.Domain;
using LinkAudioPlayer.TestSupport;
using Xunit;

namespace LinkAudioPlayer.Backup.Tests;

public sealed class BackupServiceTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;
    private TrackStore _tracks = null!;
    private PlaylistStore _playlists = null!;
    private BackupService _backup = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        _tracks = new TrackStore(_database);
        _playlists = new PlaylistStore(_database);
        _backup = new BackupService(_tracks, _playlists);
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Exports_tracks_and_playlists_and_reads_them_back()
    {
        var ct = TestContext.Current.CancellationToken;
        var track = await AddTrackAsync("a");
        var playlist = await _playlists.CreateAsync("Focus", "notes", ct);
        await _playlists.AddTrackAsync(playlist!.Id, track, ct);

        var json = await _backup.ExportAsync(ct);

        Assert.Contains("LinkAudioPlayer", json, StringComparison.Ordinal);
        Assert.Contains("https://example.com/a", json, StringComparison.Ordinal);
        Assert.Contains("Focus", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Importing_into_an_empty_library_restores_everything()
    {
        var ct = TestContext.Current.CancellationToken;
        var json = await BuildBackupAsync();

        await using var fresh = await SqliteTestDatabase.CreateAsync(ct);
        var service = new BackupService(new TrackStore(fresh), new PlaylistStore(fresh));

        var summary = await service.ImportAsync(json, ct);

        Assert.Equal(2, summary.TracksAdded);
        Assert.Equal(1, summary.PlaylistsCreated);
        Assert.Equal(2, summary.PlaylistEntriesAdded);

        var restored = await new TrackStore(fresh).ListAsync(ct);
        Assert.Equal(2, restored.Count);
    }

    // A link already in the library must not be added a second time.
    [Fact]
    public async Task Importing_the_same_backup_twice_adds_nothing_the_second_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var json = await BuildBackupAsync();

        await using var fresh = await SqliteTestDatabase.CreateAsync(ct);
        var service = new BackupService(new TrackStore(fresh), new PlaylistStore(fresh));

        await service.ImportAsync(json, ct);
        var second = await service.ImportAsync(json, ct);

        Assert.Equal(0, second.TracksAdded);
        Assert.Equal(2, second.TracksSkipped);
        Assert.Equal(0, second.PlaylistsCreated);
        Assert.Equal(0, second.PlaylistEntriesAdded);
        Assert.True(second.IsEmpty);
    }

    [Fact]
    public async Task Importing_tops_up_a_playlist_that_already_exists()
    {
        var ct = TestContext.Current.CancellationToken;
        var json = await BuildBackupAsync();

        await using var fresh = await SqliteTestDatabase.CreateAsync(ct);
        var playlists = new PlaylistStore(fresh);
        await playlists.CreateAsync("Focus", null, ct);

        var summary = await new BackupService(new TrackStore(fresh), playlists).ImportAsync(json, ct);

        Assert.Equal(0, summary.PlaylistsCreated);
        Assert.Equal(2, summary.PlaylistEntriesAdded);
    }

    // The same link in two playlists is fine and must survive a round trip.
    [Fact]
    public async Task The_same_track_may_sit_in_more_than_one_playlist()
    {
        var ct = TestContext.Current.CancellationToken;
        var track = await AddTrackAsync("shared");

        foreach (var name in (string[])["One", "Two"])
        {
            var created = await _playlists.CreateAsync(name, null, ct);
            await _playlists.AddTrackAsync(created!.Id, track, ct);
        }

        var json = await _backup.ExportAsync(ct);

        await using var fresh = await SqliteTestDatabase.CreateAsync(ct);
        var summary = await new BackupService(new TrackStore(fresh), new PlaylistStore(fresh))
            .ImportAsync(json, ct);

        Assert.Equal(1, summary.TracksAdded);
        Assert.Equal(2, summary.PlaylistsCreated);
        Assert.Equal(2, summary.PlaylistEntriesAdded);
    }

    [Fact]
    public async Task Refuses_a_file_that_is_not_one_of_ours()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _backup.ImportAsync("""{"app":"SomethingElse","version":1}""", ct));
    }

    [Fact]
    public async Task Refuses_a_backup_from_a_newer_format()
    {
        var ct = TestContext.Current.CancellationToken;

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            _backup.ImportAsync("""{"app":"LinkAudioPlayer","version":99}""", ct));

        Assert.Contains("newer version", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_text_that_is_not_json()
    {
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidDataException>(() => _backup.ImportAsync("not json at all", ct));
    }

    /// <summary>Two tracks in one playlist, exported.</summary>
    private async Task<string> BuildBackupAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = await AddTrackAsync("a");
        var second = await AddTrackAsync("b");

        var playlist = await _playlists.CreateAsync("Focus", null, ct);
        await _playlists.AddTrackAsync(playlist!.Id, first, ct);
        await _playlists.AddTrackAsync(playlist.Id, second, ct);

        return await _backup.ExportAsync(ct);
    }

    private async Task<Guid> AddTrackAsync(string title)
    {
        var track = await _tracks.AddAsync(
            new Track
            {
                SourceUrl = $"https://example.com/{title}",
                Provider = "test",
                Title = title,
                CacheKey = title,
            },
            TestContext.Current.CancellationToken);

        return track.Id;
    }
}
