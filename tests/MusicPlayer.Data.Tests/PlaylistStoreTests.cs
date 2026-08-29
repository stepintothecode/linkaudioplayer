using MusicPlayer.Data;
using MusicPlayer.Domain;
using MusicPlayer.TestSupport;
using Xunit;

namespace MusicPlayer.Data.Tests;

// Each store call gets its own context, matching the per request scope on the server.
public sealed class PlaylistStoreTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async ValueTask InitializeAsync() =>
        _database = await SqliteTestDatabase.CreateAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Appends_tracks_in_order()
    {
        var playlistId = await CreatePlaylistAsync();
        await AppendAsync(playlistId, await AddTrackAsync("a"));
        await AppendAsync(playlistId, await AddTrackAsync("b"));

        var loaded = await GetAsync(playlistId);

        Assert.Equal([0, 1], loaded!.Items.Select(i => i.Position));
        Assert.Equal(["a", "b"], loaded.Items.Select(i => i.Track!.Title));
    }

    // Used to leave a hole at position 2 because the test shared one change tracker.
    [Fact]
    public async Task Closes_the_gap_after_a_removal()
    {
        var playlistId = await CreatePlaylistAsync();
        foreach (var title in (string[])["a", "b", "c"])
        {
            await AppendAsync(playlistId, await AddTrackAsync(title));
        }

        var loaded = await GetAsync(playlistId);
        var middle = loaded!.Items.Single(i => i.Track!.Title == "b");

        await using (var context = _database.NewContext())
        {
            await new PlaylistStore(context).RemoveItemAsync(
                playlistId,
                middle.Id,
                TestContext.Current.CancellationToken);
        }

        var after = await GetAsync(playlistId);

        Assert.Equal([0, 1], after!.Items.Select(i => i.Position));
        Assert.Equal(["a", "c"], after.Items.Select(i => i.Track!.Title));
    }

    [Fact]
    public async Task Reorders_to_the_requested_sequence()
    {
        var playlistId = await CreatePlaylistAsync();
        foreach (var title in (string[])["a", "b", "c"])
        {
            await AppendAsync(playlistId, await AddTrackAsync(title));
        }

        var loaded = await GetAsync(playlistId);
        var reversed = loaded!.Items.OrderByDescending(i => i.Position).Select(i => i.Id).ToList();

        await using (var context = _database.NewContext())
        {
            Assert.True(await new PlaylistStore(context).ReorderAsync(
                playlistId,
                reversed,
                TestContext.Current.CancellationToken));
        }

        var after = await GetAsync(playlistId);
        Assert.Equal(["c", "b", "a"], after!.Items.Select(i => i.Track!.Title));
    }

    [Fact]
    public async Task Refuses_a_reorder_that_does_not_name_every_item()
    {
        var playlistId = await CreatePlaylistAsync();
        await AppendAsync(playlistId, await AddTrackAsync("a"));
        await AppendAsync(playlistId, await AddTrackAsync("b"));

        var loaded = await GetAsync(playlistId);
        var partial = loaded!.Items.Take(1).Select(i => i.Id).ToList();

        await using var context = _database.NewContext();
        Assert.False(await new PlaylistStore(context).ReorderAsync(
            playlistId,
            partial,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Will_not_add_a_track_that_does_not_exist()
    {
        var playlistId = await CreatePlaylistAsync();

        await using var context = _database.NewContext();
        Assert.False(await new PlaylistStore(context).AddTrackAsync(
            playlistId,
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Deleting_a_playlist_leaves_its_tracks_in_the_library()
    {
        var playlistId = await CreatePlaylistAsync();
        var trackId = await AddTrackAsync("a");
        await AppendAsync(playlistId, trackId);

        await using (var context = _database.NewContext())
        {
            await new PlaylistStore(context).DeleteAsync(playlistId, TestContext.Current.CancellationToken);
        }

        Assert.Null(await GetAsync(playlistId));

        await using var check = _database.NewContext();
        Assert.NotNull(await new TrackStore(check).GetAsync(trackId, TestContext.Current.CancellationToken));
    }

    private async Task<Guid> CreatePlaylistAsync()
    {
        await using var context = _database.NewContext();
        var playlist = await new PlaylistStore(context).CreateAsync(
            "List",
            null,
            TestContext.Current.CancellationToken);

        return playlist.Id;
    }

    private async Task<Playlist?> GetAsync(Guid playlistId)
    {
        await using var context = _database.NewContext();
        return await new PlaylistStore(context).GetAsync(playlistId, TestContext.Current.CancellationToken);
    }

    private async Task AppendAsync(Guid playlistId, Guid trackId)
    {
        await using var context = _database.NewContext();
        Assert.True(await new PlaylistStore(context).AddTrackAsync(
            playlistId,
            trackId,
            TestContext.Current.CancellationToken));
    }

    private async Task<Guid> AddTrackAsync(string title)
    {
        await using var context = _database.NewContext();
        var track = await new TrackStore(context).AddAsync(
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
