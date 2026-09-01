using LinkAudioPlayer.Data;
using LinkAudioPlayer.Domain;
using LinkAudioPlayer.Library;
using LinkAudioPlayer.TestSupport;
using Xunit;

namespace LinkAudioPlayer.Library.Tests;

// Undo has to restore position as well as existence. A safety net that puts a track
// back at the wrong place is only half a safety net.
public sealed class RemovalServiceTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;
    private TrackStore _tracks = null!;
    private PlaylistStore _playlists = null!;
    private RemovalService _removal = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        _tracks = new TrackStore(_database);
        _playlists = new PlaylistStore(_database);
        _removal = new RemovalService(_tracks, _playlists);
    }

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Undoing_a_track_restores_its_place_in_the_library()
    {
        var ct = TestContext.Current.CancellationToken;
        await AddTrackAsync("a");
        var middle = await AddTrackAsync("b");
        await AddTrackAsync("c");

        var before = (await _tracks.ListAsync(ct)).Select(t => t.Title).ToList();

        var removed = await _removal.RemoveTrackAsync(middle, ct);
        Assert.NotNull(removed);
        Assert.Equal(2, (await _tracks.ListAsync(ct)).Count);

        await _removal.RestoreAsync(removed, ct);

        Assert.Equal(before, (await _tracks.ListAsync(ct)).Select(t => t.Title));
    }

    [Fact]
    public async Task Undoing_a_track_restores_its_place_in_every_playlist()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = await AddTrackAsync("a");
        var middle = await AddTrackAsync("b");
        var last = await AddTrackAsync("c");

        var playlist = await _playlists.CreateAsync("Focus", null, ct);
        foreach (var id in (Guid[])[first, middle, last])
        {
            await _playlists.AddTrackAsync(playlist!.Id, id, ct);
        }

        var removed = await _removal.RemoveTrackAsync(middle, ct);
        await _removal.RestoreAsync(removed!, ct);

        var loaded = await _playlists.GetAsync(playlist!.Id, ct);
        Assert.Equal(
            ["a", "b", "c"],
            loaded!.Items.OrderBy(i => i.Position).Select(i => i.Track!.Title));
    }

    [Fact]
    public async Task Removing_a_track_takes_it_out_of_its_playlists()
    {
        var ct = TestContext.Current.CancellationToken;
        var track = await AddTrackAsync("a");
        var playlist = await _playlists.CreateAsync("Focus", null, ct);
        await _playlists.AddTrackAsync(playlist!.Id, track, ct);

        await _removal.RemoveTrackAsync(track, ct);

        var loaded = await _playlists.GetAsync(playlist.Id, ct);
        Assert.Empty(loaded!.Items);
    }

    [Fact]
    public async Task Removing_a_track_that_is_already_gone_returns_nothing()
    {
        var ct = TestContext.Current.CancellationToken;

        Assert.Null(await _removal.RemoveTrackAsync(Guid.CreateVersion7(), ct));
    }

    [Fact]
    public async Task Undoing_a_playlist_restores_its_tracks_in_order()
    {
        var ct = TestContext.Current.CancellationToken;
        var ids = new List<Guid>();
        foreach (var title in (string[])["a", "b", "c"])
        {
            ids.Add(await AddTrackAsync(title));
        }

        var playlist = await _playlists.CreateAsync("Focus", "notes", ct);
        foreach (var id in ids)
        {
            await _playlists.AddTrackAsync(playlist!.Id, id, ct);
        }

        var removed = await _removal.RemovePlaylistAsync(playlist!.Id, ct);
        Assert.NotNull(removed);
        Assert.Empty(await _playlists.ListAsync(ct));

        await _removal.RestoreAsync(removed, ct);

        var restored = Assert.Single(await _playlists.ListAsync(ct));
        Assert.Equal("Focus", restored.Name);
        Assert.Equal("notes", restored.Description);

        var loaded = await _playlists.GetAsync(restored.Id, ct);
        Assert.Equal(
            ["a", "b", "c"],
            loaded!.Items.OrderBy(i => i.Position).Select(i => i.Track!.Title));
    }

    [Fact]
    public async Task Deleting_a_playlist_leaves_its_tracks_in_the_library()
    {
        var ct = TestContext.Current.CancellationToken;
        var track = await AddTrackAsync("a");
        var playlist = await _playlists.CreateAsync("Focus", null, ct);
        await _playlists.AddTrackAsync(playlist!.Id, track, ct);

        await _removal.RemovePlaylistAsync(playlist.Id, ct);

        Assert.Single(await _tracks.ListAsync(ct));
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
