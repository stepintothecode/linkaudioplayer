using Microsoft.Extensions.Logging;

namespace LinkAudioPlayer.Diagnostics;

/// <summary>
/// Ring buffer of recent log entries, so the app can show its own console.
/// Registered as a singleton and deliberately survives a renderer teardown, which is
/// exactly when the entries matter most.
/// </summary>
public sealed class InMemoryLogStore
{
    private readonly Lock _gate = new();
    private readonly Queue<LogEntry> _entries = new();
    private readonly int _capacity;

    /// <summary>Creates the store.</summary>
    /// <param name="capacity">Entries kept before the oldest are dropped.</param>
    public InMemoryLogStore(int capacity = 500) => _capacity = capacity;

    /// <summary>Raised whenever an entry is added or the store is cleared.</summary>
    public event Action? Changed;

    /// <summary>Records an entry, dropping the oldest once at capacity.</summary>
    public void Add(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > _capacity)
            {
                _entries.Dequeue();
            }
        }

        Changed?.Invoke();
    }

    /// <summary>Returns the entries, newest first.</summary>
    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            return [.. _entries.Reverse()];
        }
    }

    /// <summary>Number of entries at or above the given level.</summary>
    public int CountAtLeast(LogLevel level)
    {
        lock (_gate)
        {
            return _entries.Count(e => e.Level >= level);
        }
    }

    /// <summary>Drops every entry.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }

        Changed?.Invoke();
    }

    /// <summary>Renders the whole buffer as text, oldest first, for copying out.</summary>
    public string ToPlainText()
    {
        LogEntry[] ordered;

        lock (_gate)
        {
            ordered = [.. _entries];
        }

        return string.Join(Environment.NewLine + Environment.NewLine, ordered.Select(e => e.ToPlainText()));
    }
}
