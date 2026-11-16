namespace LinkAudioPlayer.Ui.Services;

/// <summary>What a press of the system back button should do.</summary>
public enum BackKind
{
    /// <summary>Show "press back again to exit".</summary>
    ShowExitHint,

    /// <summary>Leave the app.</summary>
    Exit,

    /// <summary>Go to <see cref="BackAction.Route"/>.</summary>
    GoTo,
}

/// <summary>The outcome of one back press.</summary>
/// <param name="Kind">What to do.</param>
/// <param name="Route">Where to go, for <see cref="BackKind.GoTo"/>.</param>
public readonly record struct BackAction(BackKind Kind, string? Route = null);

/// <summary>
/// Decides what the system back button does. On a tab it asks for a second press
/// before leaving, so one stray press never closes the app. Anywhere else it returns
/// to the page you came from. Kept apart from the layout so the rules are testable.
/// </summary>
public sealed class BackNavigation
{
    /// <summary>How long the second press has to arrive in.</summary>
    public static readonly TimeSpan ExitWindow = TimeSpan.FromSeconds(2);

    private const int MaxHistory = 50;

    private readonly List<string> _history = [];
    private readonly TimeProvider _clock;
    private DateTimeOffset? _hintShownAt;

    /// <summary>Creates the rules over a clock, the system one by default.</summary>
    public BackNavigation(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    /// <summary>
    /// Records a page being shown. Arriving at the page before the current one is
    /// treated as going back, so the trail shrinks rather than growing forever.
    /// </summary>
    public void Visited(string path)
    {
        var normalised = TabNavigation.Normalise(path);
        _hintShownAt = null;

        if (_history.Count >= 2 && _history[^2] == normalised)
        {
            _history.RemoveAt(_history.Count - 1);
            return;
        }

        if (_history.Count > 0 && _history[^1] == normalised)
        {
            return;
        }

        _history.Add(normalised);
        if (_history.Count > MaxHistory)
        {
            _history.RemoveAt(0);
        }
    }

    /// <summary>Works out what a back press on the given page does.</summary>
    public BackAction Press(string currentPath)
    {
        var normalised = TabNavigation.Normalise(currentPath);

        if (TabNavigation.IsTab(normalised))
        {
            var now = _clock.GetUtcNow();
            if (_hintShownAt is { } shown && now - shown <= ExitWindow)
            {
                _hintShownAt = null;
                return new BackAction(BackKind.Exit);
            }

            _hintShownAt = now;
            return new BackAction(BackKind.ShowExitHint);
        }

        // The page you came from, or the tab this page sits under when the trail is
        // empty, such as straight after the app reopened onto a playlist.
        var earlier = _history.Count >= 2 && _history[^1] == normalised
            ? _history[^2]
            : TabNavigation.ParentOf(normalised) ?? TabNavigation.Tabs[0];

        return new BackAction(BackKind.GoTo, earlier);
    }
}
