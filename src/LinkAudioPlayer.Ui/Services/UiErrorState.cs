using Microsoft.Extensions.Logging;

namespace LinkAudioPlayer.Ui.Services;

/// <summary>
/// The one place the UI reports failures. Every handler routes here rather than
/// letting an exception escape, because an escaped exception tears down the Blazor
/// renderer and leaves the whole app dead until it is restarted.
/// </summary>
public sealed class UiErrorState
{
    private readonly ILogger<UiErrorState> _logger;

    /// <summary>Creates the state over the logger that feeds the log viewer.</summary>
    public UiErrorState(ILogger<UiErrorState> logger) => _logger = logger;

    /// <summary>Raised when the current error changes.</summary>
    public event Action? Changed;

    /// <summary>Message to show, or null when there is nothing to report.</summary>
    public string? Message { get; private set; }

    /// <summary>Records a failure and logs it with its stack trace.</summary>
    /// <param name="action">What was being attempted, used in the message.</param>
    /// <param name="exception">The failure.</param>
    public void Report(string action, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        _logger.LogError(exception, "{Action} failed", action);
        Message = $"{action} failed: {Describe(exception)}";
        Changed?.Invoke();
    }

    /// <summary>Clears the current error.</summary>
    public void Clear()
    {
        if (Message is null)
        {
            return;
        }

        Message = null;
        Changed?.Invoke();
    }

    /// <summary>
    /// Runs work, reporting any failure instead of letting it escape.
    /// Returns false when the work failed.
    /// </summary>
    public async Task<bool> TryAsync(string action, Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        Clear();

        try
        {
            await work();
            return true;
        }
        catch (Exception ex)
        {
            Report(action, ex);
            return false;
        }
    }

    /// <summary>Innermost message, which is usually the useful one.</summary>
    private static string Describe(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }
}
