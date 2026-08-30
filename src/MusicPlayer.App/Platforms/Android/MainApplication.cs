using Android.App;
using Android.Runtime;

namespace MusicPlayer.App;

/// <summary>Android application entry point.</summary>
[Application]
public sealed class MainApplication : MauiApplication
{
    /// <summary>Required constructor for the Android runtime.</summary>
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
