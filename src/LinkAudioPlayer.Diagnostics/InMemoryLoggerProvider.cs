using Microsoft.Extensions.Logging;

namespace LinkAudioPlayer.Diagnostics;

/// <summary>Logging provider that writes into an <see cref="InMemoryLogStore"/>.</summary>
public sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly InMemoryLogStore _store;

    /// <summary>Creates the provider over a store.</summary>
    public InMemoryLoggerProvider(InMemoryLogStore store) => _store = store;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(_store, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>Logger that appends formatted entries to the store.</summary>
    private sealed class InMemoryLogger : ILogger
    {
        private readonly InMemoryLogStore _store;
        private readonly string _category;

        public InMemoryLogger(InMemoryLogStore store, string category)
        {
            _store = store;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _store.Add(new LogEntry(
                DateTimeOffset.Now,
                logLevel,
                _category,
                formatter(state, exception),
                exception?.ToString()));
        }
    }
}
