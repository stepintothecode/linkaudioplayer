using System.Collections.Concurrent;
using Android.App;
using Android.Content;

namespace LinkAudioPlayer.App.Platforms.Android;

/// <summary>
/// Turns a system dialog launched for a result into an awaitable task.
/// <see cref="MainActivity"/> forwards every result here.
/// </summary>
internal static class ActivityResults
{
    private static readonly ConcurrentDictionary<int, TaskCompletionSource<Intent?>> Pending = new();
    private static int _sequence;

    /// <summary>Starts the intent. Completes with the returned data, or null when cancelled.</summary>
    public static Task<Intent?> StartAsync(Intent intent)
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("The app is not in the foreground.");

        // Request codes must fit in 16 bits, so they cycle through a small range.
        var code = 4000 + (Interlocked.Increment(ref _sequence) % 1000);
        var completion = new TaskCompletionSource<Intent?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Pending[code] = completion;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                activity.StartActivityForResult(intent, code);
            }
            catch (Exception ex)
            {
                Pending.TryRemove(code, out _);
                completion.TrySetException(ex);
            }
        });

        return completion.Task;
    }

    /// <summary>Completes the matching request. False when the code is not one of ours.</summary>
    public static bool Complete(int requestCode, Result resultCode, Intent? data)
    {
        if (!Pending.TryRemove(requestCode, out var completion))
        {
            return false;
        }

        completion.TrySetResult(resultCode == Result.Ok ? data : null);
        return true;
    }
}
