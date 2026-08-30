using System.Collections.Concurrent;

namespace LinkAudioPlayer.TestSupport;

// Stands in for Android's main thread context: a context that pumps posted work
// on one dedicated thread. Anything that resumes onto it is, on a real device,
// running on the UI thread.
public sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];
    private readonly Thread _thread;

    public SingleThreadSynchronizationContext()
    {
        _thread = new Thread(Pump) { IsBackground = true, Name = "fake-ui" };
        _thread.Start();
    }

    /// Work items posted to this context, which is what a UI thread resumption looks like.
    public int PostCount { get; private set; }

    public override void Post(SendOrPostCallback d, object? state)
    {
        PostCount++;
        _queue.Add((d, state));
    }

    public override void Send(SendOrPostCallback d, object? state) => d(state);

    /// Runs work with this context installed, the way a UI event handler would.
    public Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        var completion = new TaskCompletionSource<T>();

        Post(
            async _ =>
            {
                try
                {
                    completion.SetResult(await work());
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            },
            null);

        return completion.Task;
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(5));
        _queue.Dispose();
    }

    private void Pump()
    {
        SetSynchronizationContext(this);

        foreach (var (callback, state) in _queue.GetConsumingEnumerable())
        {
            callback(state);
        }
    }
}
