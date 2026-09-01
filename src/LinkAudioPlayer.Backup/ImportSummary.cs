namespace LinkAudioPlayer.Backup;

/// <summary>What an import actually changed.</summary>
public sealed class ImportSummary
{
    /// <summary>Tracks new to the library.</summary>
    public int TracksAdded { get; set; }

    /// <summary>Tracks already present, left untouched.</summary>
    public int TracksSkipped { get; set; }

    /// <summary>Playlists that did not exist before.</summary>
    public int PlaylistsCreated { get; set; }

    /// <summary>Entries added across all playlists.</summary>
    public int PlaylistEntriesAdded { get; set; }

    /// <summary>True when the file changed nothing.</summary>
    public bool IsEmpty =>
        TracksAdded == 0 && PlaylistsCreated == 0 && PlaylistEntriesAdded == 0;

    /// <summary>One line description, for a toast.</summary>
    public override string ToString() =>
        IsEmpty
            ? "Everything in that backup was already here"
            : $"Added {TracksAdded} track(s) and {PlaylistEntriesAdded} playlist entr(ies)";
}
