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
    public async Task A_later_success_clears_an_earlier_failure()
    {
        await _errors.TryAsync("First", () => throw new InvalidOperationException("boom"));
        Assert.NotNull(_errors.Message);

        await _errors.TryAsync("Second", () => Task.CompletedTask);
        Assert.Null(_errors.Message);
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
