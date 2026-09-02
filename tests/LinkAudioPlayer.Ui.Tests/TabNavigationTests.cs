using LinkAudioPlayer.Ui.Services;
using Xunit;

namespace LinkAudioPlayer.Ui.Tests;

// Swiping must never land somewhere unexpected, and must do nothing at all on a
// sub page, where it would otherwise compete with the system back gesture.
public sealed class TabNavigationTests
{
    private const string Detail = "playlists/0199b0d2-1234-7000-8000-000000000000";

    [Theory]
    [InlineData("", "playlists")]
    [InlineData("playlists", "about")]
    public void Swiping_forward_moves_to_the_next_tab(string from, string expected) =>
        Assert.Equal(expected, TabNavigation.Next(from, 1));

    [Theory]
    [InlineData("about", "playlists")]
    [InlineData("playlists", "")]
    public void Swiping_back_moves_to_the_previous_tab(string from, string expected) =>
        Assert.Equal(expected, TabNavigation.Next(from, -1));

    [Fact]
    public void Swiping_past_the_last_tab_does_nothing() =>
        Assert.Null(TabNavigation.Next("about", 1));

    [Fact]
    public void Swiping_before_the_first_tab_does_nothing() =>
        Assert.Null(TabNavigation.Next("", -1));

    // Inside a playlist, swiping back returns to the list rather than skipping past
    // it to the library, which is what the back button on that screen does too.
    [Fact]
    public void Swiping_back_out_of_a_playlist_returns_to_the_list() =>
        Assert.Equal("playlists", TabNavigation.Next(Detail, -1));

    [Fact]
    public void Swiping_forward_out_of_a_playlist_moves_to_the_next_tab() =>
        Assert.Equal("about", TabNavigation.Next(Detail, 1));

    [Theory]
    [InlineData("something-else")]
    [InlineData("nowhere/deep")]
    public void Swiping_on_an_unknown_route_does_nothing(string path)
    {
        Assert.Null(TabNavigation.Next(path, 1));
        Assert.Null(TabNavigation.Next(path, -1));
    }

    [Fact]
    public void A_path_with_slashes_or_a_query_still_matches_its_tab()
    {
        Assert.Equal("about", TabNavigation.Next("/playlists/", 1));
        Assert.Equal("about", TabNavigation.Next("playlists?x=1", 1));
    }

    [Theory]
    [InlineData("", "playlists", "from-right")]
    [InlineData("playlists", "about", "from-right")]
    [InlineData("about", "", "from-left")]
    [InlineData("playlists", "", "from-left")]
    public void Slides_in_from_the_side_it_came_from(string from, string to, string expected) =>
        Assert.Equal(expected, TabNavigation.SlideClass(from, to));

    [Fact]
    public void Staying_put_does_not_slide() =>
        Assert.Equal(string.Empty, TabNavigation.SlideClass("playlists", "playlists"));

    [Fact]
    public void Opening_a_playlist_slides_forward() =>
        Assert.Equal("from-right", TabNavigation.SlideClass("playlists", Detail));

    [Fact]
    public void Leaving_a_playlist_slides_back() =>
        Assert.Equal("from-left", TabNavigation.SlideClass(Detail, "playlists"));

    [Fact]
    public void Moving_from_a_playlist_to_a_later_tab_slides_forward() =>
        Assert.Equal("from-right", TabNavigation.SlideClass(Detail, "about"));

    [Theory]
    [InlineData("/playlists/", "playlists")]
    [InlineData("about?from=x", "about")]
    [InlineData("", "")]
    public void Normalise_strips_slashes_and_the_query(string input, string expected) =>
        Assert.Equal(expected, TabNavigation.Normalise(input));

    [Theory]
    [InlineData("", 0)]
    [InlineData("playlists", 1)]
    [InlineData("about", 2)]
    public void Reports_where_a_tab_sits(string path, int expected) =>
        Assert.Equal(expected, TabNavigation.IndexOf(path));

    // A drilled in page reports its parent, so the ends of the strip still resist.
    [Fact]
    public void A_playlist_page_reports_its_parent_tab() =>
        Assert.Equal(1, TabNavigation.IndexOf(Detail));

    [Fact]
    public void An_unknown_route_has_no_position() =>
        Assert.Equal(-1, TabNavigation.IndexOf("nowhere"));

    [Fact]
    public void Count_matches_the_tab_strip() =>
        Assert.Equal(TabNavigation.Tabs.Length, TabNavigation.Count);
}
