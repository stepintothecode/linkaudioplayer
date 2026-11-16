namespace LinkAudioPlayer.Domain;

/// <summary>
/// A folder on the phone that holds music, shown as a read only playlist.
/// Only its order is ours to change. Its songs come and go with the files.
/// </summary>
public sealed class DeviceFolder
{
    /// <summary>Stable local identifier, so a route survives a rescan.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Path relative to the storage root, such as "Music/Coldplay". Unique.</summary>
    public required string Path { get; set; }

    /// <summary>Last segment of the path, which is what the user sees.</summary>
    public required string Name { get; set; }

    /// <summary>Sort order among folders, ascending, dense from zero.</summary>
    public int Position { get; set; }

    /// <summary>Name shown for files sitting directly in the storage root.</summary>
    public const string RootName = "Phone storage";

    /// <summary>"Music/Coldplay/" -> "Coldplay". An empty path is the storage root.</summary>
    public static string NameFor(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var trimmed = path.Trim('/');
        if (trimmed.Length == 0)
        {
            return RootName;
        }

        var cut = trimmed.LastIndexOf('/');
        return cut < 0 ? trimmed : trimmed[(cut + 1)..];
    }

    /// <summary>
    /// Folder names that hold recordings or phone sounds rather than music, compared
    /// lowercased with spaces, dashes and underscores removed.
    /// </summary>
    private static readonly HashSet<string> NotMusic =
    [
        "voicerecorder", "soundrecorder", "callrecorder", "callrecordings", "recordings",
        "voicenotes", "whatsappvoicenotes", "ringtones", "notifications", "alarms",
    ];

    /// <summary>
    /// True when any folder on the path holds recordings or phone sounds.
    /// Android's own flags miss these when an older recorder saved outside the standard
    /// Recordings folder, as Samsung's did with "VoiceRecorder" and "Music/Voice Recorder".
    /// </summary>
    public static bool IsNotMusic(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Normalise(path)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => new string([.. segment.Where(char.IsLetterOrDigit)]).ToLowerInvariant())
            .Any(NotMusic.Contains);
    }

    /// <summary>Normalises a folder path to "A/B", without leading or trailing slashes.</summary>
    public static string Normalise(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Replace('\\', '/').Trim('/');
    }
}
