using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicPlayer.Abstractions;
using MusicPlayer.App.Platforms.Android;
using MusicPlayer.Data;
using MusicPlayer.Extraction;
using MusicPlayer.Library;
using MusicPlayer.Playback;
using MusicPlayer.Storage;
using YoutubeExplode;

namespace MusicPlayer.App;

/// <summary>Composition root. Wires every layer into the MAUI container.</summary>
public static class MauiProgram
{
    /// <summary>Builds and configures the application.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        AddStorage(builder.Services);
        AddExtraction(builder.Services);
        AddPlayback(builder.Services);

        var app = builder.Build();
        InitialiseDatabase(app.Services);

        return app;
    }

    /// <summary>Database and media cache, both under the app's private data directory.</summary>
    private static void AddStorage(IServiceCollection services)
    {
        var dataDirectory = FileSystem.AppDataDirectory;

        services.AddDbContextFactory<LibraryDbContext>(options =>
            options.UseSqlite($"Data Source={Path.Combine(dataDirectory, "library.db")}"));

        services.AddScoped<IPlaylistStore, PlaylistStore>();
        services.AddScoped<ITrackStore, TrackStore>();

        services.Configure<MediaCacheOptions>(options =>
            options.Directory = Path.Combine(dataDirectory, "media"));

        services.AddSingleton<MediaDownloadService>();
        services.AddSingleton<IMediaCache, FileSystemMediaCache>();
    }

    /// <summary>Source policy, resolvers and downloaders.</summary>
    private static void AddExtraction(IServiceCollection services)
    {
        services.AddSingleton(new HttpClient());
        services.AddSingleton(sp => new YoutubeClient(sp.GetRequiredService<HttpClient>()));

        services.AddSingleton<ISourcePolicy, DrmSourcePolicy>();

        // Order does not matter. The services sort by Priority.
        services.AddSingleton<IMediaResolver, DirectUrlResolver>();
        services.AddSingleton<IMediaResolver, YoutubeResolver>();
        services.AddSingleton<IMediaDownloader, DirectUrlDownloader>();
        services.AddSingleton<IMediaDownloader, YoutubeDownloader>();

        services.AddSingleton<MediaResolutionService>();
        services.AddScoped<TrackLibraryService>();
    }

    /// <summary>Platform audio and the queue controller, both long lived.</summary>
    private static void AddPlayback(IServiceCollection services)
    {
        services.AddSingleton<IAudioPlayer, AndroidAudioPlayer>();

        // Singleton so the queue survives navigation between pages.
        services.AddSingleton<PlayerController>();
    }

    /// <summary>Creates the SQLite schema before the first screen renders.</summary>
    private static void InitialiseDatabase(IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<LibraryDbContext>>();
        DatabaseInitializer.InitializeAsync(factory).GetAwaiter().GetResult();
    }
}
