namespace LinkAudioPlayer.Ui.Services;

/// <summary>An audio file another app asked this one to play.</summary>
/// <param name="Uri">Content URI, or an absolute path for an old style file link.</param>
/// <param name="Title">Name to show, from the file name.</param>
public sealed record OpenedAudio(string Uri, string Title);

/// <summary>
/// Holds an audio file opened from another app, such as "Open with" in a file manager,
/// until the layout plays it. A singleton, because the request arrives on the platform
/// side, possibly before any page exists.
/// </summary>
public sealed class OpenedAudioInbox
{
    private readonly Lock _gate = new();
    private OpenedAudio? _pending;

    /// <summary>Raised when a file arrives.</summary>
    public event Action? Arrived;

    /// <summary>Accepts a file to play.</summary>
    public void Deliver(OpenedAudio file)
    {
        ArgumentNullException.ThrowIfNull(file);

        lock (_gate)
        {
            _pending = file;
        }

        Arrived?.Invoke();
    }

    /// <summary>The waiting file, once. Null when nothing is waiting.</summary>
    public OpenedAudio? Take()
    {
        lock (_gate)
        {
            var file = _pending;
            _pending = null;
            return file;
        }
    }
}
