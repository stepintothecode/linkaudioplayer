namespace LinkAudioPlayer.Domain;

/// <summary>Join row placing a track at a position within a playlist.</summary>
public sealed class PlaylistItem
{
    /// <summary>Stable local identifier.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Owning playlist.</summary>
    public Guid PlaylistId { get; set; }

    /// <summary>Navigation to the owning playlist.</summary>
    public Playlist? Playlist { get; set; }

    /// <summary>Referenced track.</summary>
    public Guid TrackId { get; set; }

    /// <summary>Navigation to the referenced track.</summary>
    public Track? Track { get; set; }

    /// <summary>Zero based sort order within the playlist.</summary>
    public int Position { get; set; }
}
