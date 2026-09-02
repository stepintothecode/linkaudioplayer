using LinkAudioPlayer.Ui.Services;
using Xunit;

namespace LinkAudioPlayer.Ui.Tests;

// Returning to the playlists tab used to land on the list, losing the playlist you
// had open.
public sealed class TabMemoryTests
{
    private static readonly Guid Id = Guid.Parse("0199b0d2-1234-7000-8000-000000000000");

    private readonly TabMemory _memory = new();

    [Fact]
    public void The_tab_leads_to_the_list_until_a_playlist_is_opened()
    {
        Assert.Equal("playlists", _memory.Resolve("playlists"));

        _memory.OpenedPlaylist(Id);

        Assert.Equal($"playlists/{Id}", _memory.Resolve("playlists"));
    }

    [Fact]
    public void Seeing_the_list_forgets_the_open_playlist()
    {
        _memory.OpenedPlaylist(Id);
        _memory.ClosedPlaylist();

        Assert.Equal("playlists", _memory.Resolve("playlists"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("about")]
    public void Other_tabs_are_untouched(string tab)
    {
        _memory.OpenedPlaylist(Id);

        Assert.Equal(tab, _memory.Resolve(tab));
    }

    [Fact]
    public void Opening_another_playlist_replaces_the_memory()
    {
        var other = Guid.Parse("0199b0d2-9999-7000-8000-000000000000");

        _memory.OpenedPlaylist(Id);
        _memory.OpenedPlaylist(other);

        Assert.Equal($"playlists/{other}", _memory.Resolve("playlists"));
    }
}
