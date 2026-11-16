using System.Text;

namespace LinkAudioPlayer.Library;

/// <summary>Turns a track title into a file name every Android file system accepts.</summary>
public static class SafeFileName
{
    /// <summary>Longest name kept, before the extension. Long titles are cut, not refused.</summary>
    public const int MaxLength = 100;

    /// <summary>Used when nothing printable is left.</summary>
    public const string Fallback = "audio";

    private const string Forbidden = "\\/:*?\"<>|";

    /// <summary>"AC/DC: Live?" -> "AC_DC_ Live_". Blank input gives <see cref="Fallback"/>.</summary>
    public static string From(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Fallback;
        }

        var builder = new StringBuilder(title.Length);
        foreach (var c in title)
        {
            builder.Append(char.IsControl(c) || Forbidden.Contains(c) ? '_' : c);
        }

        // Trailing dots and spaces are dropped by some providers, which then mangle the name.
        var name = builder.ToString().Trim().TrimEnd('.', ' ');

        if (name.Length > MaxLength)
        {
            // Never split a surrogate pair, or the name ends in an invalid character.
            var cut = char.IsHighSurrogate(name[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
            name = name[..cut].TrimEnd('.', ' ');
        }

        return name.Length == 0 ? Fallback : name;
    }
}
