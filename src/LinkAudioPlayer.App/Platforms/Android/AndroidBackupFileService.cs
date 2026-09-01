using LinkAudioPlayer.Abstractions;
using Microsoft.Extensions.Logging;

namespace LinkAudioPlayer.App.Platforms.Android;

/// <summary>
/// Hands a backup to the Android share sheet and reads one back through the picker.
/// Sharing rather than writing to storage keeps the app free of storage permissions:
/// the user chooses Drive, Files or anywhere else themselves.
/// </summary>
public sealed class AndroidBackupFileService : IBackupFileService
{
    private readonly ILogger<AndroidBackupFileService> _logger;

    /// <summary>Creates the service.</summary>
    public AndroidBackupFileService(ILogger<AndroidBackupFileService> logger) => _logger = logger;

    /// <inheritdoc />
    public async Task<bool> SaveAsync(string content, string fileName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        // Written to the cache first. The share sheet needs a real file to hand over.
        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(path, content, cancellationToken);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "LinkAudioPlayer backup",
            File = new ShareFile(path),
        });

        _logger.LogInformation("Offered backup {File} of {Bytes} bytes", fileName, content.Length);
        return true;
    }

    /// <inheritdoc />
    public async Task<string?> OpenAsync(CancellationToken cancellationToken)
    {
        // Some file providers report a backup as octet-stream rather than JSON, so the
        // filter stays wide and the content is validated when it is parsed.
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choose a LinkAudioPlayer backup",
        });

        if (result is null)
        {
            return null;
        }

        await using var stream = await result.OpenReadAsync();
        using var reader = new StreamReader(stream);

        var text = await reader.ReadToEndAsync(cancellationToken);
        _logger.LogInformation("Read backup {File} of {Bytes} bytes", result.FileName, text.Length);

        return text;
    }
}
