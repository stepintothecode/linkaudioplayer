namespace MusicPlayer.Abstractions;

/// <summary>Fetches audio for a source URL onto local disk.</summary>
public interface IMediaDownloader
{
    /// <summary>Downloads audio and returns the absolute path of the written file.</summary>
    /// <param name="source">URL to fetch.</param>
    /// <param name="destinationDirectory">Directory to write into. Created when missing.</param>
    /// <param name="fileNameStem">File name without extension. The downloader picks the extension.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    Task<string> DownloadAsync(
        Uri source,
        string destinationDirectory,
        string fileNameStem,
        CancellationToken cancellationToken);
}
