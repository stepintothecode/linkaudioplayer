using LinkAudioPlayer.Data;
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

    // Used to throw NotSupportedException: SQLite cannot ORDER BY a DateTimeOffset column.
    [Fact]
    public async Task Lists_tracks_newest_first()
    {
        var now = DateTimeOffset.UtcNow;
        await AddAsync("oldest", now.AddHours(-2));
        await AddAsync("newest", now);
        await AddAsync("middle", now.AddHours(-1));

        var tracks = await _store.ListAsync(TestContext.Current.CancellationToken);

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
