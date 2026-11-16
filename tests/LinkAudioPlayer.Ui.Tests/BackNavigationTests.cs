using LinkAudioPlayer.TestSupport;
using LinkAudioPlayer.Ui.Services;
using Xunit;

namespace LinkAudioPlayer.Ui.Tests;

// One stray back press on a tab must never close the app, and back inside a page
// must return to where you came from rather than jumping somewhere else.
public sealed class BackNavigationTests
{
    private const string Playlist = "playlists/0199b0d2-1234-7000-8000-000000000000";

    private readonly ManualClock _clock = new();
    private readonly BackNavigation _back;

    public BackNavigationTests() => _back = new BackNavigation(_clock);

    [Theory]
    [InlineData("")]
    [InlineData("playlists")]
    [InlineData("about")]
    public void The_first_press_on_a_tab_only_warns(string tab)
    {
        _back.Visited(tab);

        Assert.Equal(BackKind.ShowExitHint, _back.Press(tab).Kind);
    }

    [Fact]
    public void A_second_press_soon_after_exits()
    {
        _back.Visited("");
        _back.Press("");
        _clock.Now += TimeSpan.FromSeconds(1);

        Assert.Equal(BackKind.Exit, _back.Press("").Kind);
    }

    [Fact]
    public void A_second_press_too_late_warns_again()
    {
        _back.Visited("");
        _back.Press("");
        _clock.Now += BackNavigation.ExitWindow + TimeSpan.FromSeconds(1);

        Assert.Equal(BackKind.ShowExitHint, _back.Press("").Kind);
    }

    [Fact]
    public void Moving_to_another_page_cancels_a_pending_exit()
    {
        _back.Visited("");
        _back.Press("");
        _back.Visited("playlists");

        Assert.Equal(BackKind.ShowExitHint, _back.Press("playlists").Kind);
    }

    [Fact]
    public void Back_from_a_playlist_returns_to_the_page_it_was_opened_from()
    {
        _back.Visited("");
        _back.Visited("playlists");
        _back.Visited(Playlist);

        Assert.Equal(new BackAction(BackKind.GoTo, "playlists"), _back.Press(Playlist));
    }

    [Fact]
    public void Back_from_a_playlist_reopened_by_the_tab_returns_to_the_tab_before()
    {
        // Tapping the playlists tab from the library reopens the remembered playlist.
        _back.Visited("");
        _back.Visited(Playlist);

        Assert.Equal(new BackAction(BackKind.GoTo, ""), _back.Press(Playlist));
    }

    [Fact]
    public void Returning_shortens_the_trail_so_back_keeps_walking_backwards()
    {
        _back.Visited("playlists");
        _back.Visited(Playlist);
        _back.Visited("playlists/device/0199b0d2-0000-7000-8000-000000000001");
        _back.Visited(Playlist);

        Assert.Equal(new BackAction(BackKind.GoTo, "playlists"), _back.Press(Playlist));
    }

    [Fact]
    public void With_no_trail_a_page_goes_back_to_its_own_tab()
    {
        _back.Visited(Playlist);

        Assert.Equal(new BackAction(BackKind.GoTo, "playlists"), _back.Press(Playlist));
    }
}
