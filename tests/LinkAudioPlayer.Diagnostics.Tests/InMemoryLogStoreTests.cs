using LinkAudioPlayer.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LinkAudioPlayer.Diagnostics.Tests;

public sealed class InMemoryLogStoreTests
{
    [Fact]
    public void Returns_entries_newest_first()
    {
        var store = new InMemoryLogStore();
        store.Add(Entry("one"));
        store.Add(Entry("two"));

        Assert.Equal(["two", "one"], store.Snapshot().Select(e => e.Message));
    }

    [Fact]
    public void Drops_the_oldest_once_at_capacity()
    {
        var store = new InMemoryLogStore(capacity: 3);

        foreach (var i in Enumerable.Range(1, 5))
        {
            store.Add(Entry(i.ToString()));
        }

        Assert.Equal(["5", "4", "3"], store.Snapshot().Select(e => e.Message));
    }

    [Fact]
    public void Counts_entries_at_or_above_a_level()
    {
        var store = new InMemoryLogStore();
        store.Add(Entry("info", LogLevel.Information));
        store.Add(Entry("warn", LogLevel.Warning));
        store.Add(Entry("err", LogLevel.Error));

        Assert.Equal(2, store.CountAtLeast(LogLevel.Warning));
    }

    [Fact]
    public void Plain_text_is_oldest_first_and_keeps_the_stack_trace()
    {
        var store = new InMemoryLogStore();
        store.Add(Entry("first"));
        store.Add(Entry("second") with { Exception = "System.Exception: boom\n   at Thing()" });

        var text = store.ToPlainText();

        Assert.True(text.IndexOf("first", StringComparison.Ordinal) < text.IndexOf("second", StringComparison.Ordinal));
        Assert.Contains("at Thing()", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Clearing_empties_the_buffer_and_notifies()
    {
        var store = new InMemoryLogStore();
        var raised = 0;
        store.Changed += () => raised++;

        store.Add(Entry("one"));
        store.Clear();

        Assert.Empty(store.Snapshot());
        Assert.Equal(2, raised);
    }

    [Fact]
    public void Short_category_drops_the_namespace()
    {
        var entry = Entry("m") with { Category = "LinkAudioPlayer.Playback.PlayerController" };

        Assert.Equal("PlayerController", entry.ShortCategory);
    }

    [Fact]
    public void Logger_provider_captures_the_exception_text()
    {
        var store = new InMemoryLogStore();
        var logger = new InMemoryLoggerProvider(store).CreateLogger("Test");

        logger.LogError(new InvalidOperationException("boom"), "while doing {Thing}", "work");

        var entry = Assert.Single(store.Snapshot());
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("while doing work", entry.Message);
        Assert.Contains("InvalidOperationException", entry.Exception!, StringComparison.Ordinal);
        Assert.Contains("boom", entry.Exception!, StringComparison.Ordinal);
    }

    private static LogEntry Entry(string message, LogLevel level = LogLevel.Information) =>
        new(DateTimeOffset.Now, level, "Test", message, null);
}
