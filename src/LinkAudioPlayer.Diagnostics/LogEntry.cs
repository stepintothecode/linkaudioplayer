using Microsoft.Extensions.Logging;

namespace LinkAudioPlayer.Diagnostics;

/// <summary>One captured log line, with the exception detail kept intact.</summary>
/// <param name="Timestamp">When the entry was recorded.</param>
/// <param name="Level">Severity.</param>
/// <param name="Category">Logger category, usually the type that logged it.</param>
/// <param name="Message">Formatted message.</param>
/// <param name="Exception">Full exception text including stack trace, or null.</param>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Category,
    string Message,
    string? Exception)
{
    /// <summary>Short category name, without the namespace.</summary>
    public string ShortCategory
    {
        get
        {
            var index = Category.LastIndexOf('.');
            return index >= 0 && index < Category.Length - 1 ? Category[(index + 1)..] : Category;
        }
    }

    /// <summary>Renders the entry the way a console would.</summary>
    public string ToPlainText() =>
        $"{Timestamp:HH:mm:ss.fff} [{Level}] {Category}{Environment.NewLine}" +
        $"  {Message}" +
        (Exception is null ? string.Empty : Environment.NewLine + Exception);
}
