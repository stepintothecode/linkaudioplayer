namespace LinkAudioPlayer.Ui.Services;

/// <summary>
/// Asks the user to confirm something destructive and waits for the answer.
/// Rendered by <c>ConfirmDialog</c> in the layout rather than using the browser's
/// own confirm, which an Android web view styles unpredictably and cannot be themed.
/// </summary>
public sealed class ConfirmService
{
    private TaskCompletionSource<bool>? _pending;

    /// <summary>Raised when the dialog should appear or disappear.</summary>
    public event Action? Changed;

    /// <summary>The question being asked, or null when nothing is pending.</summary>
    public ConfirmRequest? Request { get; private set; }

    /// <summary>Shows the dialog and completes once the user answers.</summary>
    /// <param name="title">Short heading.</param>
    /// <param name="message">What will happen.</param>
    /// <param name="confirmLabel">Label on the confirming button.</param>
    public Task<bool> AskAsync(string title, string message, string confirmLabel = "Delete")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        // A second question replaces the first, which is then treated as declined.
        _pending?.TrySetResult(false);

        _pending = new TaskCompletionSource<bool>();
        Request = new ConfirmRequest(title, message, confirmLabel);
        Changed?.Invoke();

        return _pending.Task;
    }

    /// <summary>Answers the pending question.</summary>
    public void Answer(bool confirmed)
    {
        var pending = _pending;
        _pending = null;
        Request = null;

        Changed?.Invoke();
        pending?.TrySetResult(confirmed);
    }
}

/// <summary>One pending confirmation.</summary>
/// <param name="Title">Short heading.</param>
/// <param name="Message">What will happen.</param>
/// <param name="ConfirmLabel">Label on the confirming button.</param>
public sealed record ConfirmRequest(string Title, string Message, string ConfirmLabel);
