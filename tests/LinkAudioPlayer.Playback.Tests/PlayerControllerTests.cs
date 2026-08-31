using Microsoft.Extensions.Logging.Abstractions;
using LinkAudioPlayer.Domain;
using LinkAudioPlayer.Playback;
using LinkAudioPlayer.TestSupport;
using Xunit;

namespace LinkAudioPlayer.Playback.Tests;

// The queue rules are the part of playback that cannot be checked on a device
// without a person watching, so they are covered here against a fake player.
public sealed class PlayerControllerTests : IAsyncDisposable
{
    private readonly FakeAudioPlayer _player = new();
    private readonly FakeMediaCache _cache = new();
    private readonly PlayerController _controller;

    public PlayerControllerTests() =>
        _controller = new PlayerController(_player, _cache, NullLogger<PlayerController>.Instance);

    public async ValueTask DisposeAsync() => await _controller.DisposeAsync();

    [Fact]
    public async Task Starts_at_the_requested_index()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b", "c"), startIndex: 1);

        Assert.Equal(1, _controller.CurrentIndex);
        Assert.Equal("b", _controller.Current!.Title);
        Assert.True(_controller.IsPlaying);
    }

    [Fact]
    public async Task Clamps_an_out_of_range_start_index()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b"), startIndex: 99);

        Assert.Equal(1, _controller.CurrentIndex);
    }

    [Fact]
    public async Task Downloads_a_track_that_is_not_cached_yet()
    {
        await _controller.PlayQueueAsync(Tracks("a"));

        Assert.Equal(["a"], _cache.Downloaded);
        Assert.Single(_player.LoadedPaths);
    }

    [Fact]
    public async Task Skips_the_download_when_already_cached()
    {
        _cache.Add("a");

        await _controller.PlayQueueAsync(Tracks("a"));

        Assert.Empty(_cache.Downloaded);
        Assert.Single(_player.LoadedPaths);
    }

    [Fact]
    public async Task Advances_when_a_track_finishes()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b"));

        _player.RaiseCompleted();
        await WaitForIndex(1);

        Assert.Equal("b", _controller.Current!.Title);
    }

    [Fact]
    public async Task Stops_at_the_end_of_the_queue_when_repeat_is_off()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b"), startIndex: 1);

        _player.RaiseCompleted();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(1, _controller.CurrentIndex);
        Assert.False(_controller.IsPlaying);
    }

    [Fact]
    public async Task Wraps_to_the_start_when_repeating_all()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b"), startIndex: 1);
        _controller.CycleRepeat();

        Assert.Equal(RepeatMode.All, _controller.Repeat);

        _player.RaiseCompleted();
        await WaitForIndex(0);

        Assert.Equal("a", _controller.Current!.Title);
    }

    [Fact]
    public async Task Replays_the_same_track_when_repeating_one()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b"));
        _controller.CycleRepeat();
        _controller.CycleRepeat();

        Assert.Equal(RepeatMode.One, _controller.Repeat);

        _player.RaiseCompleted();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(0, _controller.CurrentIndex);
        Assert.Equal(2, _player.LoadedPaths.Count);
    }

    [Fact]
    public async Task A_manual_skip_ignores_repeat_one()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b"));
        _controller.CycleRepeat();
        _controller.CycleRepeat();

        await _controller.NextAsync();

        Assert.Equal(1, _controller.CurrentIndex);
    }

    [Fact]
    public async Task Previous_restarts_the_track_when_past_three_seconds()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b"), startIndex: 1);
        _player.AdvanceTo(TimeSpan.FromSeconds(10));

        await _controller.PreviousAsync();

        Assert.Equal(1, _controller.CurrentIndex);
        Assert.Equal(TimeSpan.Zero, _player.Position);
    }

    [Fact]
    public async Task Previous_steps_back_when_near_the_start()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b"), startIndex: 1);
        _player.AdvanceTo(TimeSpan.FromSeconds(1));

        await _controller.PreviousAsync();

        Assert.Equal(0, _controller.CurrentIndex);
    }

    [Fact]
    public async Task Previous_on_the_first_track_restarts_it()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b"));
        _player.AdvanceTo(TimeSpan.FromSeconds(1));

        await _controller.PreviousAsync();

        Assert.Equal(0, _controller.CurrentIndex);
    }

    [Fact]
    public async Task Lock_screen_next_advances_the_queue()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b"));

        _player.RaiseNextRequested();
        await WaitForIndex(1);

        Assert.Equal("b", _controller.Current!.Title);
    }

    [Fact]
    public async Task Lock_screen_previous_steps_back()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b"), startIndex: 1);

        _player.RaisePreviousRequested();
        await WaitForIndex(0);

        Assert.Equal("a", _controller.Current!.Title);
    }

    [Fact]
    public async Task Toggling_pauses_and_resumes()
    {
        await _controller.PlayQueueAsync(Tracks("a"));

        await _controller.TogglePlayPauseAsync();
        Assert.False(_controller.IsPlaying);

        await _controller.TogglePlayPauseAsync();
        Assert.True(_controller.IsPlaying);
    }

    [Fact]
    public async Task Stopping_clears_the_queue()
    {
        await _controller.PlayQueueAsync(Tracks("a", "b"));

        await _controller.StopAsync();

        Assert.Empty(_controller.Queue);
        Assert.Null(_controller.Current);
        Assert.Equal(-1, _controller.CurrentIndex);
        Assert.Equal(1, _player.StopCount);
    }

    [Fact]
    public async Task Surfaces_a_download_failure_without_throwing()
    {
        _cache.StoreFailure = new InvalidOperationException("no audio stream");

        await _controller.PlayQueueAsync(Tracks("a"));

        Assert.Equal("no audio stream", _controller.Error);
        Assert.Empty(_player.LoadedPaths);
        Assert.False(_controller.IsPreparing);
    }

    [Fact]
    public async Task Surfaces_a_playback_failure()
    {
        await _controller.PlayQueueAsync(Tracks("a"));

        _player.RaiseFailed("codec not supported");

        Assert.Equal("codec not supported", _controller.Error);
        Assert.False(_controller.IsPlaying);
    }

    [Fact]
    public async Task Passes_lock_screen_metadata_to_the_platform()
    {
        var track = new Track
        {
            SourceUrl = "https://example.com/a",
            Provider = "test",
            Title = "Song",
            Artist = "Artist",
            DurationSeconds = 210,
            ThumbnailUrl = "https://example.com/art.jpg",
            CacheKey = "a",
        };

        await _controller.PlayQueueAsync([track]);

        var metadata = _player.LastMetadata;
        Assert.NotNull(metadata);
        Assert.Equal("Song", metadata.Title);
        Assert.Equal("Artist", metadata.Artist);
        Assert.Equal(TimeSpan.FromSeconds(210), metadata.Duration);
        Assert.Equal("https://example.com/art.jpg", metadata.ArtworkUrl);
    }

    [Fact]
    public async Task An_empty_queue_leaves_nothing_playing()
    {
        await _controller.PlayQueueAsync([]);

        Assert.Equal(-1, _controller.CurrentIndex);
        Assert.Null(_controller.Current);
        Assert.Empty(_player.LoadedPaths);
    }

    [Fact]
    public async Task Skipping_mid_download_abandons_the_first_track()
    {
        _cache.StoreGate = new TaskCompletionSource();
        _cache.Add("b");

        var first = _controller.PlayQueueAsync(Tracks("a", "b"));
        await _controller.NextAsync();

        _cache.StoreGate.SetResult();
        await first;

        // Only the track actually reached should have been handed to the player.
        Assert.Equal(["/cache/b.webm"], _player.LoadedPaths);
    }

    private static List<Track> Tracks(params string[] titles) =>
        [.. titles.Select(title => new Track
        {
            SourceUrl = $"https://example.com/{title}",
            Provider = "test",
            Title = title,
            CacheKey = title,
        })];

    /// <summary>
    /// The completion and lock screen handlers are async void, so state settles a tick later.
    /// </summary>
    private async Task WaitForIndex(int expected)
    {
        for (var attempt = 0; attempt < 50 && _controller.CurrentIndex != expected; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Shuffle_keeps_the_current_track_playing()
    {
        await _controller.PlayQueueAsync(Tracks(6), startIndex: 3);
        var before = _controller.Current;

        _controller.ToggleShuffle();

        Assert.True(_controller.IsShuffled);
        Assert.Same(before, _controller.Current);
    }

    [Fact]
    public async Task Shuffle_visits_every_track_exactly_once()
    {
        await _controller.PlayQueueAsync(Tracks(8));
        _controller.ToggleShuffle();

        var seen = new List<Guid> { _controller.Current!.Id };
        for (var i = 0; i < 7; i++)
        {
            await _controller.NextAsync();
            seen.Add(_controller.Current!.Id);
        }

        Assert.Equal(8, seen.Distinct().Count());
    }

    [Fact]
    public async Task Turning_shuffle_off_restores_the_queue_order()
    {
        await _controller.PlayQueueAsync(Tracks(5), startIndex: 2);
        _controller.ToggleShuffle();
        _controller.ToggleShuffle();

        Assert.False(_controller.IsShuffled);
        Assert.Equal(2, _controller.CurrentIndex);

        await _controller.NextAsync();
        Assert.Equal(3, _controller.CurrentIndex);
    }

    [Fact]
    public async Task Shuffle_before_anything_is_queued_is_harmless()
    {
        _controller.ToggleShuffle();
        await _controller.PlayQueueAsync(Tracks(3));

        Assert.True(_controller.IsShuffled);
        Assert.NotNull(_controller.Current);
    }

    private static List<Track> Tracks(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new Track
        {
            SourceUrl = $"https://example.com/{i}",
            Provider = "test",
            Title = $"track {i}",
            CacheKey = $"key{i}",
        })];
}
