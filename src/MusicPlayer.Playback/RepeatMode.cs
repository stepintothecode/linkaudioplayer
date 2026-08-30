namespace MusicPlayer.Playback;

/// <summary>What happens when a track finishes.</summary>
public enum RepeatMode
{
    /// <summary>Advance, and stop at the end of the queue.</summary>
    Off = 0,

    /// <summary>Advance, wrapping to the start at the end of the queue.</summary>
    All = 1,

    /// <summary>Replay the current track.</summary>
    One = 2,
}
