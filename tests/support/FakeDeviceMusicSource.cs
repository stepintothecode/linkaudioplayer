using LinkAudioPlayer.Abstractions;

namespace LinkAudioPlayer.TestSupport;

// The phone's media index, as a list the test controls.
public sealed class FakeDeviceMusicSource : IDeviceMusicSource
{
    public bool HasAccess { get; set; } = true;

    /// What the next scan returns.
    public List<DeviceSong> Songs { get; } = [];

    /// How many scans ran.
    public int Scans { get; private set; }

    /// Holds a scan open until the test releases it.
    public TaskCompletionSource? ScanGate { get; set; }

    public Task<bool> RequestAccessAsync() => Task.FromResult(HasAccess);

    public void OpenSettings()
    {
    }

    public async Task<IReadOnlyList<DeviceSong>> ScanAsync(CancellationToken cancellationToken)
    {
        Scans++;

        if (ScanGate is not null)
        {
            await ScanGate.Task.WaitAsync(cancellationToken);
        }

        return [.. Songs];
    }
}
