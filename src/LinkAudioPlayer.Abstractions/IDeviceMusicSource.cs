namespace LinkAudioPlayer.Abstractions;

/// <summary>
/// Reads the music already on the phone. Read only by design: the app plays these
/// files and never writes, moves or deletes them.
/// </summary>
public interface IDeviceMusicSource
{
    /// <summary>True when the user has allowed access to music files.</summary>
    bool HasAccess { get; }

    /// <summary>Asks for access. False when refused, including a permanent refusal.</summary>
    Task<bool> RequestAccessAsync();

    /// <summary>Opens the app's system settings, where a permanent refusal can be undone.</summary>
    void OpenSettings();

    /// <summary>Lists every music file. Ringtones, alarms and recordings are left out.</summary>
    Task<IReadOnlyList<DeviceSong>> ScanAsync(CancellationToken cancellationToken);
}
