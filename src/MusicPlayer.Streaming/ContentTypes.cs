namespace MusicPlayer.Streaming;

/// <summary>Maps audio file extensions to MIME types for range responses.</summary>
public static class ContentTypes
{
    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp3"] = "audio/mpeg",
        [".m4a"] = "audio/mp4",
        [".m4b"] = "audio/mp4",
        [".aac"] = "audio/aac",
        [".ogg"] = "audio/ogg",
        [".oga"] = "audio/ogg",
        [".opus"] = "audio/ogg",
        [".flac"] = "audio/flac",
        [".wav"] = "audio/wav",
        [".webm"] = "audio/webm",
        [".mp4"] = "video/mp4",
    };

    /// <summary>Returns the MIME type for a file path, defaulting to a generic binary type.</summary>
    public static string ForPath(string path)
    {
        var extension = Path.GetExtension(path);
        return ByExtension.TryGetValue(extension, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
}
