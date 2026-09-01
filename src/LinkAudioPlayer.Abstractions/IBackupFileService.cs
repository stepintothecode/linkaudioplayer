namespace LinkAudioPlayer.Abstractions;

/// <summary>
/// Hands a backup to the platform and takes one back.
/// Kept behind an interface because saving and picking a file are MAUI concerns and
/// the UI is a plain Razor library.
/// </summary>
public interface IBackupFileService
{
    /// <summary>Offers the content to the user to save or share. False when they cancelled.</summary>
    Task<bool> SaveAsync(string content, string fileName, CancellationToken cancellationToken);

    /// <summary>Asks the user for a backup file and returns its text, or null when cancelled.</summary>
    Task<string?> OpenAsync(CancellationToken cancellationToken);
}
