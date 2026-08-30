namespace MusicPlayer.App;

/// <summary>MAUI application shell.</summary>
public partial class App : Application
{
    /// <summary>Creates the application and loads its resources.</summary>
    public App() => InitializeComponent();

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new MainPage()) { Title = "Music Player" };
}
