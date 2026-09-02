using System.Text.Json;
using LinkAudioPlayer.Abstractions;
using LinkAudioPlayer.Domain;

namespace LinkAudioPlayer.Backup;

/// <summary>Writes the whole library and every playlist to a file, and reads one back.</summary>
public sealed class BackupService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ITrackStore _tracks;
    private readonly IPlaylistStore _playlists;

    /// <summary>Creates the service over the two stores.</summary>
    public BackupService(ITrackStore tracks, IPlaylistStore playlists)
    {
        _tracks = tracks;
        _playlists = playlists;
    }

    /// <summary>Serialises everything to indented JSON.</summary>
    public async Task<string> ExportAsync(CancellationToken cancellationToken)
    {
        var document = new BackupDocument();

        foreach (var track in await _tracks.ListAsync(cancellationToken))
        {
            document.Tracks.Add(new BackupTrack
            {
                SourceUrl = track.SourceUrl,
                Provider = track.Provider,
                Title = track.Title,
                Artist = track.Artist,
                DurationSeconds = track.DurationSeconds,
                ThumbnailUrl = track.ThumbnailUrl,
            });
        }

        foreach (var summary in await _playlists.ListAsync(cancellationToken))
        {
            var playlist = await _playlists.GetAsync(summary.Id, cancellationToken);
            if (playlist is null)
            {
                continue;
            }

            document.Playlists.Add(new BackupPlaylist
            {
                Name = playlist.Name,
                Description = playlist.Description,
                TrackUrls =
                [
                    .. playlist.Items
                        .OrderBy(item => item.Position)
                        .Select(item => item.Track?.SourceUrl)
                        .OfType<string>(),
                ],
            });
        }

        return JsonSerializer.Serialize(document, Json);
    }

    /// <summary>
    /// Merges a backup into the current data.
    /// Nothing is replaced or removed. A track already in the library is left alone,
    /// and a playlist that already exists gains only the entries it is missing. The
    /// same link may appear in any number of playlists.
    /// </summary>
    public async Task<ImportSummary> ImportAsync(string json, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var document = Parse(json);
        var summary = new ImportSummary();

        // Source URL to track id, for the playlist pass. Seeded with what is already here.
        var byUrl = (await _tracks.ListAsync(cancellationToken))
            .ToDictionary(track => track.SourceUrl, track => track.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in document.Tracks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(entry.SourceUrl))
            {
                continue;
            }

            if (byUrl.ContainsKey(entry.SourceUrl))
            {
                summary.TracksSkipped++;
                continue;
            }

            var added = await _tracks.AddAsync(ToTrack(entry), cancellationToken);
            byUrl[added.SourceUrl] = added.Id;
            summary.TracksAdded++;
        }

        // Oldest first, so the newest ends up at the top of the list, the same way
        // the track pass works.
        foreach (var entry in Enumerable.Reverse(document.Playlists))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ImportPlaylistAsync(entry, byUrl, summary, cancellationToken);
        }

        return summary;
    }

    /// <summary>Creates or tops up one playlist.</summary>
    private async Task ImportPlaylistAsync(
        BackupPlaylist entry,
        IReadOnlyDictionary<string, Guid> byUrl,
        ImportSummary summary,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            return;
        }

        var existing = (await _playlists.ListAsync(cancellationToken))
            .FirstOrDefault(p => string.Equals(p.Name, entry.Name.Trim(), StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            existing = await _playlists.CreateAsync(entry.Name, entry.Description, cancellationToken);
            if (existing is null)
            {
                return;
            }

            summary.PlaylistsCreated++;
        }

        var loaded = await _playlists.GetAsync(existing.Id, cancellationToken);
        var present = loaded is null
            ? []
            : loaded.Items.Select(item => item.TrackId).ToHashSet();

        foreach (var url in entry.TrackUrls)
        {
            if (!byUrl.TryGetValue(url, out var trackId) || !present.Add(trackId))
            {
                continue;
            }

            if (await _playlists.AddTrackAsync(existing.Id, trackId, cancellationToken))
            {
                summary.PlaylistEntriesAdded++;
            }
        }
    }

    /// <summary>Reads the document, rejecting anything that is not one of ours.</summary>
    private static BackupDocument Parse(string json)
    {
        BackupDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<BackupDocument>(json, Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("That file is not valid JSON.", ex);
        }

        if (document is null || !string.Equals(document.App, "LinkAudioPlayer", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("That file is not a LinkAudioPlayer backup.");
        }

        if (document.Version > BackupDocument.CurrentVersion)
        {
            throw new InvalidDataException(
                $"That backup was written by a newer version (format {document.Version}). Update the app first.");
        }

        return document;
    }

    /// <summary>Maps a backup entry onto a new track.</summary>
    private static Track ToTrack(BackupTrack entry) => new()
    {
        SourceUrl = entry.SourceUrl,
        Provider = string.IsNullOrWhiteSpace(entry.Provider) ? "unknown" : entry.Provider,
        Title = string.IsNullOrWhiteSpace(entry.Title) ? entry.SourceUrl : entry.Title,
        Artist = entry.Artist,
        DurationSeconds = entry.DurationSeconds,
        ThumbnailUrl = entry.ThumbnailUrl,
        CacheKey = CacheKeyFactory.FromSourceUrl(entry.SourceUrl),
    };
}
