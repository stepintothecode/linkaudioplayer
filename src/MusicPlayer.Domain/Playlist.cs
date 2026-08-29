namespace MusicPlayer.Domain;

/// <summary>A named, ordered collection of tracks.</summary>
public sealed class Playlist
{
    /// <summary>Stable local identifier.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Display name.</summary>
    public required string Name { get; set; }

    /// <summary>Optional free text description.</summary>
    public string? Description { get; set; }

    /// <summary>When the playlist was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When the playlist or its contents last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Ordered membership rows.</summary>
    public List<PlaylistItem> Items { get; set; } = [];
}
