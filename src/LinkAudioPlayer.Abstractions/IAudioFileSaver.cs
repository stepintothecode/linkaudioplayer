namespace LinkAudioPlayer.Abstractions;

/// <summary>
/// Lets the user pick where a file goes, then writes it there. On Android this is the
/// system save dialog, so the app needs no storage permission to write.
/// </summary>
public interface IAudioFileSaver
{
    /// <summary>
    /// Shows the save dialog. Returns an opaque handle to the chosen file, or null when
    /// the user cancelled. The file exists, empty, once this returns.
    /// </summary>
    Task<string?> PickAsync(string suggestedName, string mimeType, CancellationToken cancellationToken);

    /// <summary>Copies a local file into the picked location.</summary>
    Task WriteAsync(string handle, string sourcePath, CancellationToken cancellationToken);

    /// <summary>Removes a picked file after a failure, so no empty file is left behind.</summary>
    Task DeleteAsync(string handle);
}
