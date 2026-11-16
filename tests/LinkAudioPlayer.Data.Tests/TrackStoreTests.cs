using LinkAudioPlayer.Data;
using Microsoft.EntityFrameworkCore;
using LinkAudioPlayer.Domain;
using LinkAudioPlayer.TestSupport;
using Xunit;

namespace LinkAudioPlayer.Data.Tests;

// Runs against real SQLite, not the in-memory provider, so provider specific
// translation failures show up here rather than at runtime.
public sealed class TrackStoreTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;
    private TrackStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        _store = new TrackStore(_database);
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    // Used to throw NotSupportedException: SQLite cannot ORDER BY a DateTimeOffset
    // column. AddedAt is now only the tiebreak, so positions are flattened to reach it.
    [Fact]
    public async Task Falls_back_to_newest_first_when_positions_tie()
    {
        var now = DateTimeOffset.UtcNow;
        await AddAsync("oldest", now.AddHours(-2));
        await AddAsync("newest", now);
        await AddAsync("middle", now.AddHours(-1));

        await using (var context = _database.CreateDbContext())
        {
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE \"Tracks\" SET \"Position\" = 0;",
                TestContext.Current.CancellationToken);
        }

        var tracks = await new TrackStore(_database).ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["newest", "middle", "oldest"], tracks.Select(t => t.Title));
    }

    [Fact]
    public async Task Round_trips_the_added_timestamp()
    {
        var stamp = new DateTimeOffset(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
        var id = await AddAsync("one", stamp);

        var loaded = await _store.GetAsync(id, TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal(stamp, loaded.AddedAt);
    }

    [Fact]
    public async Task Finds_a_track_by_its_source_url()
    {
        await AddAsync("one", DateTimeOffset.UtcNow);

        var found = await _store.FindBySourceUrlAsync(
            "https://example.com/one",
            TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal("one", found.Title);
    }

    [Fact]
    public async Task Reports_whether_a_delete_matched()
    {
        var id = await AddAsync("one", DateTimeOffset.UtcNow);

        Assert.True(await _store.DeleteAsync(id, TestContext.Current.CancellationToken));
        Assert.False(await _store.DeleteAsync(id, TestContext.Current.CancellationToken));
    }

    // A new track used to be ordered by AddedAt alone, which gave no way to reorder.
    [Fact]
    public async Task A_new_track_goes_to_the_top_and_pushes_the_rest_down()
    {
        await AddAsync("first", DateTimeOffset.UtcNow);
        await AddAsync("second", DateTimeOffset.UtcNow);
        await AddAsync("third", DateTimeOffset.UtcNow);

        var tracks = await new TrackStore(_database).ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["third", "second", "first"], tracks.Select(t => t.Title));
        Assert.Equal([0, 1, 2], tracks.Select(t => t.Position));
    }

    [Fact]
    public async Task Reorders_the_library_and_renumbers_densely()
    {
        foreach (var title in (string[])["a", "b", "c"])
        {
            await AddAsync(title, DateTimeOffset.UtcNow);
        }

        var store = new TrackStore(_database);
        var current = await store.ListAsync(TestContext.Current.CancellationToken);
        var reversed = current.Reverse().Select(t => t.Id).ToList();

        Assert.True(await store.ReorderAsync(reversed, TestContext.Current.CancellationToken));

        var after = await store.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["a", "b", "c"], after.Select(t => t.Title));
        Assert.Equal([0, 1, 2], after.Select(t => t.Position));
    }

    [Fact]
    public async Task Refuses_a_reorder_that_does_not_name_every_track()
    {
        await AddAsync("a", DateTimeOffset.UtcNow);
        await AddAsync("b", DateTimeOffset.UtcNow);

        var store = new TrackStore(_database);
        var partial = (await store.ListAsync(TestContext.Current.CancellationToken))
            .Take(1)
            .Select(t => t.Id)
            .ToList();

        Assert.False(await store.ReorderAsync(partial, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Phone_songs_stay_out_of_the_library_and_cannot_be_deleted()
    {
        var ct = TestContext.Current.CancellationToken;
        await AddAsync("linked", DateTimeOffset.UtcNow);

        var folders = new DeviceFolderStore(_database);
        await folders.SyncAsync([new("content://media/1", "phone song", null, null, "Music")], ct);
        var phoneSong = Assert.Single(await folders.ListAllSongsAsync(ct));

        Assert.Equal(["linked"], (await _store.ListAsync(ct)).Select(t => t.Title));
        Assert.False(await _store.DeleteAsync(phoneSong.Id, ct));
    }

    [Fact]
    public async Task Adding_a_link_leaves_the_order_inside_phone_folders_alone()
    {
        var ct = TestContext.Current.CancellationToken;
        var folders = new DeviceFolderStore(_database);
        await folders.SyncAsync([new("content://media/1", "a", null, null, "Music"), new("content://media/2", "b", null, null, "Music")], ct);

        await AddAsync("linked", DateTimeOffset.UtcNow);

        Assert.Equal([0, 1], (await folders.ListAllSongsAsync(ct)).Select(t => t.Position));
    }

    private async Task<Guid> AddAsync(string title, DateTimeOffset addedAt)
    {
        var track = await _store.AddAsync(
            new Track
            {
                SourceUrl = $"https://example.com/{title}",
                Provider = "test",
                Title = title,
                CacheKey = title,
                AddedAt = addedAt,
            },
            TestContext.Current.CancellationToken);

        return track.Id;
    }
}
