namespace MusicPlayer.Abstractions;

/// <summary>Outcome of evaluating a source URL against the project policy.</summary>
public enum SourceVerdict
{
    /// <summary>The URL may be resolved and streamed.</summary>
    Allowed = 0,

    /// <summary>The host serves DRM protected media. Resolution is refused outright.</summary>
    BlockedDrm = 1,

    /// <summary>The URL is malformed or uses an unsupported scheme.</summary>
    Rejected = 2,
}
