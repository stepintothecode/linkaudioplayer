namespace LinkAudioPlayer.Ui.Services;

/// <summary>
/// Brief confirmations for actions whose effect is otherwise invisible, such as
/// toggling loop or shuffle. Holds one message at a time; a new one replaces the old.
/// </summary>
public sealed class ToastService
{
    private CancellationTokenSource? _dismiss;

    /// <summary>Raised when the visible message changes.</summary>
    public event Action? Changed;

    /// <summary>Message currently showing, or null.</summary>
    public string? Message { get; private set; }

    /// <summary>Icon shown beside the message, or null.</summary>
    public string? Icon { get; private set; }

    /// <summary>Shows a message for a short while.</summary>
    /// <param name="message">Text to show.</param>
    /// <param name="icon">Optional leading glyph.</param>
    /// <param name="duration">How long it stays. Defaults to just under two seconds.</param>
    public void Show(string message, string? icon = null, TimeSpan? duration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Message = message;
        Icon = icon;
        Changed?.Invoke();

        Restart(duration ?? TimeSpan.FromMilliseconds(1800));
    }

    /// <summary>Hides the current message immediately.</summary>
    public void Clear()
    {
        _dismiss?.Cancel();

        if (Message is null)
        {
            return;
        }

        Message = null;
        Icon = null;
        Changed?.Invoke();
    }

    /// <summary>Replaces any pending dismissal with a fresh one.</summary>
    private void Restart(TimeSpan duration)
    {
        _dismiss?.Cancel();
        _dismiss?.Dispose();
        _dismiss = new CancellationTokenSource();

        var token = _dismiss.Token;

        _ = Task.Delay(duration, token).ContinueWith(
            _ =>
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                Message = null;
                Icon = null;
                Changed?.Invoke();
            },
            TaskScheduler.Default);
    }
}
