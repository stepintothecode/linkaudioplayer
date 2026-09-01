using LinkAudioPlayer.Ui.Services;
using Xunit;

namespace LinkAudioPlayer.Ui.Tests;

// Swiping must never land somewhere unexpected, and must do nothing at all on a
// sub page, where it would otherwise compete with the system back gesture.
public sealed class TabNavigationTests
{
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

    // A playlist page is not a tab. Swiping there must be inert.
    [Theory]
    [InlineData("playlists/0199b0d2-1234-7000-8000-000000000000")]
    [InlineData("something-else")]
    public void Swiping_on_a_sub_page_does_nothing(string path)
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

    [Theory]
    [InlineData("playlists", "playlists/0199b0d2-1234-7000-8000-000000000000")]
    [InlineData("playlists/0199b0d2-1234-7000-8000-000000000000", "playlists")]
    public void Moving_to_or_from_a_sub_page_does_not_slide(string from, string to) =>
        Assert.Equal(string.Empty, TabNavigation.SlideClass(from, to));

    [Theory]
    [InlineData("/playlists/", "playlists")]
    [InlineData("about?from=x", "about")]
    [InlineData("", "")]
    public void Normalise_strips_slashes_and_the_query(string input, string expected) =>
        Assert.Equal(expected, TabNavigation.Normalise(input));
}
