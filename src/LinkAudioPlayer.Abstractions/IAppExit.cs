namespace LinkAudioPlayer.Abstractions;

/// <summary>Leaves the app, as a second back press on a tab does.</summary>
public interface IAppExit
{
    /// <summary>Takes the app off screen. Playback, if any, carries on.</summary>
    void Leave();
}
