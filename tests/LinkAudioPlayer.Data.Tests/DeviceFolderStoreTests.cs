using LinkAudioPlayer.Abstractions;
using LinkAudioPlayer.Data;
using LinkAudioPlayer.TestSupport;
using Xunit;

namespace LinkAudioPlayer.Data.Tests;

// A scan replaces nothing the user arranged: order survives, new songs join the end,
// and only songs whose files are gone disappear.
public sealed class DeviceFolderStoreTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;
    private DeviceFolderStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        _store = new DeviceFolderStore(_database);
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Names_a_folder_after_its_last_segment_and_keeps_same_named_folders_apart()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.SyncAsync([Song(1, "A", "DCIM/Music"), Song(2, "B", "Download/Music"), Song(3, "C", "Songs/Coldplay")], ct);

        var folders = await _store.ListAsync(ct);

        Assert.Equal(["Coldplay", "Music", "Music"], folders.Select(f => f.Folder.Name));
        Assert.Equal(["Songs/Coldplay", "DCIM/Music", "Download/Music"], folders.Select(f => f.Folder.Path));
    }

    [Fact]
    public async Task A_rescan_with_nothing_new_reports_no_change()
    {
        var ct = TestContext.Current.CancellationToken;
        var songs = new[] { Song(1, "A", "Music"), Song(2, "B", "Music") };

        Assert.True(await _store.SyncAsync(songs, ct));
        Assert.False(await _store.SyncAsync(songs, ct));
    }

    [Fact]
    public async Task Keeps_the_users_order_and_adds_new_songs_at_the_end()
    {
        var ct = TestContext.Current.CancellationToken;
        await _store.SyncAsync([Song(1, "A", "Music"), Song(2, "B", "Music")], ct);

        var folder = (await _store.ListAsync(ct)).Single().Folder;
        var songs = await _store.ListSongsAsync(folder.Id, ct);
        await _store.ReorderSongsAsync(folder.Id, [songs[1].Id, songs[0].Id], ct);

        await _store.SyncAsync([Song(1, "A", "Music"), Song(2, "B", "Music"), Song(3, "Aardvark", "Music")], ct);

        var titles = (await _store.ListSongsAsync(folder.Id, ct)).Select(s => s.Title);
        Assert.Equal(["B", "A", "Aardvark"], titles);
    }

    [Fact]
    public async Task A_deleted_file_leaves_its_folder_and_every_playlist()
    {
        var ct = TestContext.Current.CancellationToken;
        await _store.SyncAsync([Song(1, "A", "Music"), Song(2, "B", "Music")], ct);

        var playlists = new PlaylistStore(_database);
        var playlist = (await playlists.CreateAsync("Mine", null, ct))!;
        foreach (var song in await _store.ListAllSongsAsync(ct))
        {
            await playlists.AddTrackAsync(playlist.Id, song.Id, ct);
        }

        await _store.SyncAsync([Song(2, "B", "Music")], ct);

        var folder = (await _store.ListAsync(ct)).Single();
        Assert.Equal(1, folder.SongCount);

        var entry = Assert.Single((await playlists.GetAsync(playlist.Id, ct))!.Items);
        Assert.Equal("B", entry.Track!.Title);
        Assert.Equal(0, entry.Position);
    }

    [Fact]
    public async Task A_folder_left_empty_disappears()
    {
        var ct = TestContext.Current.CancellationToken;
        await _store.SyncAsync([Song(1, "A", "Music"), Song(2, "B", "Podcasts")], ct);

        await _store.SyncAsync([Song(1, "A", "Music")], ct);

        Assert.Equal(["Music"], (await _store.ListAsync(ct)).Select(f => f.Folder.Name));
    }

    [Fact]
    public async Task A_moved_file_joins_the_end_of_its_new_folder()
    {
        var ct = TestContext.Current.CancellationToken;
        await _store.SyncAsync([Song(1, "A", "Music"), Song(2, "B", "Music"), Song(3, "C", "Old")], ct);

        await _store.SyncAsync([Song(1, "A", "Music"), Song(2, "B", "Music"), Song(3, "C", "Music")], ct);

        var folder = Assert.Single(await _store.ListAsync(ct));
        Assert.Equal(["A", "B", "C"], (await _store.ListSongsAsync(folder.Folder.Id, ct)).Select(s => s.Title));
    }

    [Fact]
    public async Task Folder_order_survives_a_rescan()
    {
        var ct = TestContext.Current.CancellationToken;
        await _store.SyncAsync([Song(1, "A", "Alpha"), Song(2, "B", "Beta")], ct);

        var folders = await _store.ListAsync(ct);
        await _store.ReorderFoldersAsync([folders[1].Folder.Id, folders[0].Folder.Id], ct);
        await _store.SyncAsync([Song(1, "A", "Alpha"), Song(2, "B", "Beta"), Song(3, "C", "Gamma")], ct);

        Assert.Equal(["Beta", "Alpha", "Gamma"], (await _store.ListAsync(ct)).Select(f => f.Folder.Name));
    }

    [Fact]
    public async Task Files_in_the_storage_root_get_a_readable_name()
    {
        var ct = TestContext.Current.CancellationToken;

        await _store.SyncAsync([Song(1, "A", "")], ct);

        Assert.Equal(Domain.DeviceFolder.RootName, Assert.Single(await _store.ListAsync(ct)).Folder.Name);
    }

    private static DeviceSong Song(int id, string title, string folder) =>
        new($"content://media/external/audio/media/{id}", title, null, null, folder);
}
