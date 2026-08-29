namespace MusicPlayer.Abstractions;

/// <summary>Thrown when a source URL cannot be resolved or is refused by policy.</summary>
public sealed class MediaResolutionException : Exception
{
    /// <summary>Creates an exception carrying a policy verdict.</summary>
    /// <param name="message">Reason shown to the caller.</param>
    /// <param name="verdict">Policy verdict that caused the failure.</param>
    /// <param name="innerException">Underlying error, when any.</param>
    public MediaResolutionException(
        string message,
        SourceVerdict verdict = SourceVerdict.Rejected,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Verdict = verdict;
    }

    /// <summary>Policy verdict associated with the failure.</summary>
    public SourceVerdict Verdict { get; }
}
