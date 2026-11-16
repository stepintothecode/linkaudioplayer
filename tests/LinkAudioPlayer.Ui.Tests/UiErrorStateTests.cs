using LinkAudioPlayer.Ui.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LinkAudioPlayer.Ui.Tests;

// This is the app's safety net. An exception escaping a UI handler tears down the
// Blazor renderer and leaves every button dead, so the contract here is that
// nothing gets through.
public sealed class UiErrorStateTests
{
    private readonly UiErrorState _errors = new(NullLogger<UiErrorState>.Instance);

    [Fact]
    public async Task Swallows_a_failure_and_reports_it()
    {
        var ok = await _errors.TryAsync("Adding the link", () => throw new InvalidOperationException("boom"));

        Assert.False(ok);
        Assert.NotNull(_errors.Message);
        Assert.Contains("Adding the link", _errors.Message, StringComparison.Ordinal);
        Assert.Contains("boom", _errors.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_the_innermost_message()
    {
        var wrapped = new InvalidOperationException("outer", new HttpRequestException("no network"));

        await _errors.TryAsync("Downloading", () => throw wrapped);

        Assert.Contains("no network", _errors.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Returns_true_and_stays_quiet_when_work_succeeds()
    {
        var ok = await _errors.TryAsync("Loading", () => Task.CompletedTask);

        Assert.True(ok);
        Assert.Null(_errors.Message);
    }

    [Fact]
    public async Task A_retry_that_succeeds_clears_its_own_failure()
    {
        await _errors.TryAsync("Adding the link", () => throw new InvalidOperationException("boom"));
        Assert.NotNull(_errors.Message);

        await _errors.TryAsync("Adding the link", () => Task.CompletedTask);
        Assert.Null(_errors.Message);
    }

    // Used to clear it: any success did, and pages reload or rebind on every render, so
    // a failed add or save vanished from the banner before it could be read.
    [Fact]
    public async Task Other_work_succeeding_leaves_a_failure_visible()
    {
        await _errors.TryAsync("Saving the MP3", () => throw new InvalidOperationException("boom"));

        await _errors.TryAsync("Loading the library", () => Task.CompletedTask);
        await _errors.TryAsync("Enabling drag to reorder", () => Task.CompletedTask);

        Assert.Contains("boom", _errors.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_new_failure_replaces_the_old_one()
    {
        await _errors.TryAsync("First", () => throw new InvalidOperationException("one"));
        await _errors.TryAsync("Second", () => throw new InvalidOperationException("two"));

        Assert.Contains("two", _errors.Message!, StringComparison.Ordinal);

        // The first action succeeding now must not clear the second's message.
        await _errors.TryAsync("First", () => Task.CompletedTask);
        Assert.NotNull(_errors.Message);
    }

    [Fact]
    public async Task Raises_changed_when_a_failure_arrives_and_when_it_is_cleared()
    {
        var raised = 0;
        _errors.Changed += () => raised++;

        await _errors.TryAsync("Work", () => throw new InvalidOperationException("boom"));
        Assert.Equal(1, raised);

        _errors.Clear();
        Assert.Equal(2, raised);
    }

    [Fact]
    public void Clearing_when_already_clear_raises_nothing()
    {
        var raised = 0;
        _errors.Changed += () => raised++;

        _errors.Clear();

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task Cancellation_is_reported_rather_than_escaping()
    {
        var ok = await _errors.TryAsync("Playing", () => throw new OperationCanceledException());

        Assert.False(ok);
        Assert.NotNull(_errors.Message);
    }
}
