namespace MusicPlayer.Abstractions;

/// <summary>Fetches audio for a source URL onto local storage. One implementation per source family.</summary>
public interface IMediaDownloader
{
    /// <summary>Downloader name, for logging.</summary>
    string Name { get; }

    /// <summary>Selection order. Lower values are tried first.</summary>
    int Priority { get; }

    /// <summary>True when this downloader handles the given URL.</summary>
    bool CanDownload(Uri source);

    /// <summary>Downloads audio and returns the absolute path of the written file.</summary>
    /// <param name="source">URL to fetch.</param>
    /// <param name="destinationDirectory">Directory to write into. Created when missing.</param>
    /// <param name="fileNameStem">File name without extension. The downloader picks the extension.</param>
    /// <param name="progress">Receives values from 0 to 1, when the source reports length.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    Task<string> DownloadAsync(
        Uri source,
        string destinationDirectory,
        string fileNameStem,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}
