namespace LinkAudioPlayer.Abstractions;

/// <summary>Verdict plus a human readable reason, surfaced to the caller on refusal.</summary>
/// <param name="Verdict">Whether the source is allowed.</param>
/// <param name="Reason">Explanation shown in the UI and logs.</param>
public sealed record SourcePolicyResult(SourceVerdict Verdict, string Reason)
{
    /// <summary>True when resolution may proceed.</summary>
    public bool IsAllowed => Verdict == SourceVerdict.Allowed;

    /// <summary>Builds an allowing result.</summary>
    public static SourcePolicyResult Allow() => new(SourceVerdict.Allowed, "Allowed.");
}
