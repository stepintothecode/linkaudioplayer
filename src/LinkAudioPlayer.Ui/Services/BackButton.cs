namespace LinkAudioPlayer.Ui.Services;

/// <summary>
/// Carries the system back button from the platform into the Blazor layout, which is
/// the only place that knows the current page. A singleton, because the platform
/// side lives outside any Blazor scope.
/// </summary>
public sealed class BackButton
{
    private Func<Task>? _handler;

    /// <summary>
    /// Raised when a handler attaches. The platform re-registers its back callback then,
    /// so it sits above the one the web view adds once it is created.
    /// </summary>
    public event Action? Attached;

    /// <summary>Sets what a press does. The layout attaches once it has rendered.</summary>
    public void Attach(Func<Task> handler)
    {
        _handler = handler;
        Attached?.Invoke();
    }

    /// <summary>Removes the handler, if it is still the one attached.</summary>
    public void Detach(Func<Task> handler)
    {
        if (_handler == handler)
        {
            _handler = null;
        }
    }

    /// <summary>Handles a press. False when nothing is attached, so the system default runs.</summary>
    public bool Press()
    {
        var handler = _handler;
        if (handler is null)
        {
            return false;
        }

        _ = handler();
        return true;
    }
}
