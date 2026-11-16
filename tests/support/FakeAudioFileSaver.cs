using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.TestSupport;

// Stands in for the system save dialog. Keeps what was "written" in memory.
public sealed class FakeAudioFileSaver : IAudioFileSaver
{
    /// When true, the user dismisses the dialog.
    public bool Cancel { get; set; }

    /// Name the dialog was opened with.
    public string? SuggestedName { get; private set; }

    /// MIME type the dialog was opened with.
    public string? MimeType { get; private set; }

    /// Bytes written to the picked location, or null if nothing was.
    public byte[]? Written { get; private set; }

    /// True once the picked file was deleted after a failure.
    public bool Deleted { get; private set; }

    public Task<string?> PickAsync(string suggestedName, string mimeType, CancellationToken cancellationToken)
    {
        SuggestedName = suggestedName;
        MimeType = mimeType;
        return Task.FromResult(Cancel ? null : "content://picked/1");
    }

    public async Task WriteAsync(string handle, string sourcePath, CancellationToken cancellationToken) =>
        Written = File.Exists(sourcePath) ? await File.ReadAllBytesAsync(sourcePath, cancellationToken) : [];

    public Task DeleteAsync(string handle)
    {
        Deleted = true;
        return Task.CompletedTask;
    }
}
