using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicPlayer.Abstractions;

namespace MusicPlayer.Extraction;

/// <summary>Runs the yt-dlp executable and captures its output.</summary>
public sealed class YtDlpProcessRunner
{
    private readonly YtDlpOptions _options;
    private readonly ILogger<YtDlpProcessRunner> _logger;

    /// <summary>Creates a runner bound to the configured executable.</summary>
    public YtDlpProcessRunner(IOptions<YtDlpOptions> options, ILogger<YtDlpProcessRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Executes yt-dlp with the given arguments and waits for it to exit.</summary>
    /// <param name="arguments">Arguments passed individually, avoiding shell quoting issues.</param>
    /// <param name="timeout">Abandons the process once elapsed.</param>
    /// <param name="cancellationToken">Cancels the run and kills the process tree.</param>
    public async Task<ProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new MediaResolutionException(
                $"Could not start '{_options.ExecutablePath}'. Install yt-dlp and make sure it is on PATH, " +
                "or set YtDlp:ExecutablePath to its full path.",
                SourceVerdict.Rejected,
                ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw new MediaResolutionException(
                cancellationToken.IsCancellationRequested
                    ? "Resolution was cancelled."
                    : $"yt-dlp did not finish within {timeout.TotalSeconds:0} seconds.");
        }

        var result = new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());

        if (!result.Succeeded)
        {
            _logger.LogWarning("yt-dlp exited with {ExitCode}: {Error}", result.ExitCode, result.StandardError);
        }

        return result;
    }

    /// <summary>Kills a process tree, ignoring races where it already exited.</summary>
    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
        }
    }
}
