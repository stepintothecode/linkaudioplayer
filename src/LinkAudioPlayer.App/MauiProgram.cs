using LinkAudioPlayer.Abstractions;
using LinkAudioPlayer.App.Platforms.Android;
using LinkAudioPlayer.Backup;
using LinkAudioPlayer.Data;
using LinkAudioPlayer.Diagnostics;
using LinkAudioPlayer.Extraction;
using LinkAudioPlayer.Library;
using LinkAudioPlayer.Playback;
using LinkAudioPlayer.Storage;
using LinkAudioPlayer.Ui.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YoutubeExplode;

namespace LinkAudioPlayer.App;

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

        AddDiagnostics(builder);

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        AddStorage(builder.Services);
        AddExtraction(builder.Services);
        AddPlayback(builder.Services);

        var app = builder.Build();

        Start(app.Services);

        return app;
    }

    /// <summary>
    /// The in memory log buffer and the global exception hooks.
    /// Registered first so anything that fails during the rest of startup is captured.
    /// </summary>
    private static void AddDiagnostics(MauiAppBuilder builder)
    {
        var store = new InMemoryLogStore();

        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton<UiErrorState>();
        builder.Services.AddSingleton<ToastService>();

        builder.Logging.AddProvider(new InMemoryLoggerProvider(store));
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // Chatty at Information and drowns out anything useful.
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

#if DEBUG
        builder.Logging.AddDebug();
#endif
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
        services.AddScoped<BackupService>();
        services.AddSingleton<IBackupFileService, AndroidBackupFileService>();
        services.AddSingleton<IExternalBrowser, AndroidExternalBrowser>();
        services.AddSingleton<IMediaCache, FileSystemMediaCache>();
    }

    /// <summary>Source policy, resolvers and downloaders.</summary>
    private static void AddExtraction(IServiceCollection services)
    {
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(5) });
        services.AddSingleton(sp => new YoutubeClient(sp.GetRequiredService<HttpClient>()));

        services.AddSingleton<ISourcePolicy, DrmSourcePolicy>();

        // Order does not matter. The services sort by Priority.
        services.AddSingleton<IMediaResolver, DirectUrlResolver>();
        services.AddSingleton<IMediaResolver, YoutubeResolver>();
        services.AddSingleton<IMediaResolver, YoutubePlaylistResolver>();
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

    /// <summary>
    /// Installs the exception hooks and creates the database.
    /// Failures here are logged rather than thrown, so a bad database still lets the
    /// app open far enough to show its own logs.
    /// </summary>
    private static void Start(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        GlobalExceptionHandler.Install(logger);

        try
        {
            var factory = services.GetRequiredService<IDbContextFactory<LibraryDbContext>>();
            DatabaseInitializer.InitializeAsync(factory).GetAwaiter().GetResult();
            logger.LogInformation("Library ready at {Path}", FileSystem.AppDataDirectory);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Could not open the library database");
        }
    }
}
