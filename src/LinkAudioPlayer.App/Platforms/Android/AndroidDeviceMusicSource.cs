using Android.Content;
using Android.Content.PM;
using Android.Provider;
using LinkAudioPlayer.Abstractions;
using Microsoft.Extensions.Logging;
using Application = Android.App.Application;

namespace LinkAudioPlayer.App.Platforms.Android;

/// <summary>
/// Lists the phone's music through MediaStore, the index Android already keeps.
/// Querying it is one cursor over a table, which stays quick with thousands of songs,
/// where walking the file system would take seconds. Read access only.
/// </summary>
public sealed class AndroidDeviceMusicSource : IDeviceMusicSource
{
    /// <summary>What MediaStore reports for a missing artist tag.</summary>
    private const string UnknownTag = "<unknown>";

    /// <summary>Volume name of the phone's built in storage, as opposed to an SD card.</summary>
    private const string PrimaryVolume = "external_primary";

    /// <summary>Prefix for folders on a removable card, so they never merge with built in ones.</summary>
    private const string CardPrefix = "SD card";

    private readonly ILogger<AndroidDeviceMusicSource> _logger;

    /// <summary>Creates the source.</summary>
    public AndroidDeviceMusicSource(ILogger<AndroidDeviceMusicSource> logger) => _logger = logger;

    /// <summary>Android 13 split storage access by media type; earlier versions use one permission.</summary>
    private static string RequiredPermission =>
        OperatingSystem.IsAndroidVersionAtLeast(33)
            ? global::Android.Manifest.Permission.ReadMediaAudio
            : global::Android.Manifest.Permission.ReadExternalStorage;

    /// <inheritdoc />
    public bool HasAccess => Application.Context.CheckSelfPermission(RequiredPermission) == Permission.Granted;

    /// <inheritdoc />
    public async Task<bool> RequestAccessAsync()
    {
        var status = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<ReadMusic>);
        return status == PermissionStatus.Granted;
    }

    /// <inheritdoc />
    public void OpenSettings() => AppInfo.Current.ShowSettingsUI();

    /// <inheritdoc />
    public Task<IReadOnlyList<DeviceSong>> ScanAsync(CancellationToken cancellationToken) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    private IReadOnlyList<DeviceSong> Scan(CancellationToken cancellationToken)
    {
        if (!HasAccess)
        {
            return [];
        }

        var collection = MediaStore.Audio.Media.ExternalContentUri!;
        var modern = OperatingSystem.IsAndroidVersionAtLeast(29);

        string[] projection = modern
            ?
            [
                IBaseColumns.Id, MediaStore.IMediaColumns.Title, MediaStore.Audio.IAudioColumns.Artist,
                MediaStore.Audio.IAudioColumns.Duration, MediaStore.IMediaColumns.DisplayName,
                MediaStore.IMediaColumns.RelativePath, MediaStore.IMediaColumns.VolumeName,
            ]
            :
            [
                IBaseColumns.Id, MediaStore.IMediaColumns.Title, MediaStore.Audio.IAudioColumns.Artist,
                MediaStore.Audio.IAudioColumns.Duration, MediaStore.IMediaColumns.DisplayName,
                MediaStore.IMediaColumns.Data,
            ];

        // is_music leaves out ringtones, notification sounds, alarms and podcasts.
        // Recordings get their own flag from Android 12, and are left out too.
        var selection = OperatingSystem.IsAndroidVersionAtLeast(31)
            ? "is_music != 0 AND is_recording = 0"
            : "is_music != 0";

        using var cursor = Application.Context.ContentResolver!.Query(collection, projection, selection, null, null);
        if (cursor is null)
        {
            _logger.LogWarning("MediaStore returned no cursor");
            return [];
        }

        var root = global::Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? "/storage/emulated/0";
        var songs = new List<DeviceSong>(cursor.Count);

        while (cursor.MoveToNext())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var id = cursor.GetLong(0);
            var title = cursor.GetString(1);
            var artist = cursor.GetString(2);
            var durationMs = cursor.IsNull(3) ? 0L : cursor.GetLong(3);
            var displayName = cursor.GetString(4);

            var folder = modern
                ? FolderFromRelativePath(cursor.GetString(5), cursor.GetString(6))
                : FolderFromAbsolutePath(cursor.GetString(5), root);

            if (folder is null)
            {
                continue;
            }

            songs.Add(new DeviceSong(
                ContentUris.WithAppendedId(collection, id).ToString()!,
                string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(displayName) ?? "Untitled" : title,
                string.IsNullOrWhiteSpace(artist) || artist == UnknownTag ? null : artist,
                durationMs > 0 ? TimeSpan.FromMilliseconds(durationMs) : null,
                folder));
        }

        return songs;
    }

    /// <summary>"Music/Coldplay/" on the phone, or "SD card/Music/Coldplay" on a card.</summary>
    private static string? FolderFromRelativePath(string? relativePath, string? volume)
    {
        if (relativePath is null)
        {
            return null;
        }

        var path = relativePath.Trim('/');
        return volume is null or PrimaryVolume ? path : $"{CardPrefix}/{path}".TrimEnd('/');
    }

    /// <summary>Before Android 10 only the absolute path exists, such as /storage/emulated/0/Music/a.mp3.</summary>
    private static string? FolderFromAbsolutePath(string? absolutePath, string root)
    {
        var directory = absolutePath is null ? null : Path.GetDirectoryName(absolutePath)?.Replace('\\', '/');
        if (directory is null)
        {
            return null;
        }

        if (directory.StartsWith(root, StringComparison.Ordinal))
        {
            return directory[root.Length..].Trim('/');
        }

        // /storage/XXXX-XXXX/Music on a removable card: drop the first two segments.
        var parts = directory.Trim('/').Split('/');
        return parts.Length >= 2 && parts[0] == "storage"
            ? $"{CardPrefix}/{string.Join('/', parts.Skip(2))}".TrimEnd('/')
            : directory.Trim('/');
    }

    /// <summary>The one permission this needs, which depends on the Android version.</summary>
    private sealed class ReadMusic : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            [(RequiredPermission, true)];
    }
}
