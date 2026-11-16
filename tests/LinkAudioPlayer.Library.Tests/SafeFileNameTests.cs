using LinkAudioPlayer.Library;
using Xunit;

namespace LinkAudioPlayer.Library.Tests;

public sealed class SafeFileNameTests
{
    [Theory]
    [InlineData("AC/DC: Live?", "AC_DC_ Live_")]
    [InlineData("  Yellow  ", "Yellow")]
    [InlineData("Ends with dots...", "Ends with dots")]
    [InlineData("तुम ही हो", "तुम ही हो")]
    public void Replaces_what_a_file_system_refuses(string title, string expected) =>
        Assert.Equal(expected, SafeFileName.From(title));

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("...")]
    public void Falls_back_when_nothing_is_left(string? title) =>
        Assert.Equal(SafeFileName.Fallback, SafeFileName.From(title));

    [Fact]
    public void Cuts_a_long_title_without_splitting_an_emoji()
    {
        var title = new string('a', SafeFileName.MaxLength - 1) + "🎵🎵";

        var name = SafeFileName.From(title);

        Assert.True(name.Length <= SafeFileName.MaxLength);
        Assert.False(char.IsHighSurrogate(name[^1]));
    }
}
