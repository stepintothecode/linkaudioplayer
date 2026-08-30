using Microsoft.Extensions.Logging;

namespace LinkAudioPlayer.Diagnostics;

/// <summary>
/// Captures exceptions that escape every other handler, so they reach the log viewer
/// instead of only the system crash report.
/// </summary>
public static class GlobalExceptionHandler
{
    private static bool _installed;

    /// <summary>Hooks the runtime wide exception events. Safe to call once at startup.</summary>
    public static void Install(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (_installed)
        {
            return;
        }

        _installed = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception, terminating: {Terminating}", e.IsTerminating);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception");

            // Marked observed so a background failure does not take the process down.
            e.SetObserved();
        };
    }
}
