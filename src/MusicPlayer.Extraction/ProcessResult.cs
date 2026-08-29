namespace MusicPlayer.Extraction;

/// <summary>Captured output of a finished subprocess.</summary>
/// <param name="ExitCode">Process exit code.</param>
/// <param name="StandardOutput">Full stdout text.</param>
/// <param name="StandardError">Full stderr text.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>True when the process exited cleanly.</summary>
    public bool Succeeded => ExitCode == 0;
}
