using LinkAudioPlayer.Data;
using LinkAudioPlayer.Domain;
using LinkAudioPlayer.TestSupport;
using Xunit;

namespace LinkAudioPlayer.Data.Tests;

public sealed class PlaylistStoreTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;
    private PlaylistStore _store = null!;
    private TrackStore _tracks = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        _store = new PlaylistStore(_database);
        _tracks = new TrackStore(_database);
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    // Used to emit UPDATE instead of INSERT, because the pre-set Guid key made EF
    // read a navigation-discovered item as an existing row.
    [Fact]
    public async Task Appends_tracks_in_order()
    {
        var playlistId = await CreateAsync();
        await AppendAsync(playlistId, await AddTrackAsync("a"));
        await AppendAsync(playlistId, await AddTrackAsync("b"));

        var loaded = await _store.GetAsync(playlistId, TestContext.Current.CancellationToken);

        Assert.Equal([0, 1], loaded!.Items.Select(i => i.Position));
        Assert.Equal(["a", "b"], loaded.Items.Select(i => i.Track!.Title));
    }

    [Fact]
    public async Task Closes_the_gap_after_a_removal()
    {
        var playlistId = await CreateAsync();
        foreach (var title in (string[])["a", "b", "c"])
        {
            await AppendAsync(playlistId, await AddTrackAsync(title));
        }

        var loaded = await _store.GetAsync(playlistId, TestContext.Current.CancellationToken);
        var middle = loaded!.Items.Single(i => i.Track!.Title == "b");

        Assert.True(await _store.RemoveItemAsync(playlistId, middle.Id, TestContext.Current.CancellationToken));

        var after = await _store.GetAsync(playlistId, TestContext.Current.CancellationToken);
        Assert.Equal([0, 1], after!.Items.Select(i => i.Position));
        Assert.Equal(["a", "c"], after.Items.Select(i => i.Track!.Title));
    }

    [Fact]
    public async Task Reorders_to_the_requested_sequence()
    {
        var playlistId = await CreateAsync();
        foreach (var title in (string[])["a", "b", "c"])
        {
            await AppendAsync(playlistId, await AddTrackAsync(title));
        }

        var loaded = await _store.GetAsync(playlistId, TestContext.Current.CancellationToken);
        var reversed = loaded!.Items.OrderByDescending(i => i.Position).Select(i => i.Id).ToList();

        Assert.True(await _store.ReorderAsync(playlistId, reversed, TestContext.Current.CancellationToken));

        var after = await _store.GetAsync(playlistId, TestContext.Current.CancellationToken);
        Assert.Equal(["c", "b", "a"], after!.Items.Select(i => i.Track!.Title));
    }

    [Fact]
    public async Task Refuses_a_reorder_that_does_not_name_every_item()
    {
        var playlistId = await CreateAsync();
        await AppendAsync(playlistId, await AddTrackAsync("a"));
        await AppendAsync(playlistId, await AddTrackAsync("b"));

        var loaded = await _store.GetAsync(playlistId, TestContext.Current.CancellationToken);
        var partial = loaded!.Items.Take(1).Select(i => i.Id).ToList();

        Assert.False(await _store.ReorderAsync(playlistId, partial, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Will_not_add_a_track_that_does_not_exist()
    {
        var playlistId = await CreateAsync();

        Assert.False(await _store.AddTrackAsync(
            playlistId,
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Deleting_a_playlist_leaves_its_tracks_in_the_library()
    {
        var playlistId = await CreateAsync();
        var trackId = await AddTrackAsync("a");
        await AppendAsync(playlistId, trackId);

        await _store.DeleteAsync(playlistId, TestContext.Current.CancellationToken);

        Assert.Null(await _store.GetAsync(playlistId, TestContext.Current.CancellationToken));
        Assert.NotNull(await _tracks.GetAsync(trackId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Lists_playlists_with_their_item_counts()
    {
        var playlistId = await CreateAsync();
        await AppendAsync(playlistId, await AddTrackAsync("a"));
        await AppendAsync(playlistId, await AddTrackAsync("b"));

        var playlists = await _store.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, Assert.Single(playlists).Items.Count);
    }

    private async Task<Guid> CreateAsync()
    {
        var playlist = await _store.CreateAsync("List", null, TestContext.Current.CancellationToken);
        return playlist.Id;
    }

    private async Task AppendAsync(Guid playlistId, Guid trackId) =>
        Assert.True(await _store.AddTrackAsync(playlistId, trackId, TestContext.Current.CancellationToken));

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
