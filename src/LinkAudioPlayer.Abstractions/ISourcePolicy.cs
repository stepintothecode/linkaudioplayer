namespace LinkAudioPlayer.Abstractions;

/// <summary>
/// Gate every source URL passes through before resolution.
/// Exists to keep DRM protected services out of the pipeline. See docs/legal-boundaries.md.
/// </summary>
public interface ISourcePolicy
{
    /// <summary>Evaluates a URL and returns whether it may be resolved.</summary>
    SourcePolicyResult Evaluate(Uri uri);
}
