namespace LinkAudioPlayer.Abstractions;

/// <summary>Turns a source URL into playable metadata. One implementation per source family.</summary>
public interface IMediaResolver
{
    /// <summary>Resolver name recorded on the track.</summary>
    string Name { get; }

    /// <summary>Selection order. Lower values are tried first.</summary>
    int Priority { get; }

    /// <summary>True when this resolver handles the given URL.</summary>
    bool CanResolve(Uri uri);

    /// <summary>Extracts metadata for the URL.</summary>
    Task<MediaResolution> ResolveAsync(Uri uri, CancellationToken cancellationToken);
}
