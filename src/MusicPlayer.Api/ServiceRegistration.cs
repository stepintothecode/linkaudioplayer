using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MusicPlayer.Abstractions;
using MusicPlayer.Api.Endpoints;
using MusicPlayer.Api.Services;
using MusicPlayer.Data;
using MusicPlayer.Extraction;
using MusicPlayer.Streaming;

namespace MusicPlayer.Api;

/// <summary>Wires every layer into the dependency injection container.</summary>
public static class ServiceRegistration
{
    /// <summary>Registers data access, extraction, caching and application services.</summary>
    /// <param name="services">Container to populate.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environment">Used to resolve relative data paths against the content root.</param>
    public static IServiceCollection AddMusicPlayer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<YtDlpOptions>(configuration.GetSection(YtDlpOptions.SectionName));
        services.Configure<MediaCacheOptions>(configuration.GetSection(MediaCacheOptions.SectionName));

        var connectionString = ResolveLibraryConnectionString(configuration, environment);
        services.AddDbContext<LibraryDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IPlaylistStore, PlaylistStore>();
        services.AddScoped<ITrackStore, TrackStore>();

        services.AddSingleton<ISourcePolicy, DrmSourcePolicy>();
        services.AddSingleton<YtDlpProcessRunner>();
        services.AddSingleton<IMediaDownloader, YtDlpDownloader>();
        services.AddSingleton<IMediaCache, FileSystemMediaCache>();

        // Order does not matter here. MediaResolutionService sorts by Priority.
        services.AddSingleton<IMediaResolver, DirectUrlResolver>();
        services.AddSingleton<IMediaResolver, YtDlpResolver>();
        services.AddSingleton<MediaResolutionService>();

        services.AddScoped<TrackLibraryService>();

        services.AddExceptionHandler<ResolutionExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }

    /// <summary>
    /// Rewrites a relative Data Source to an absolute path under the content root, so the
    /// database lands in the same place regardless of the working directory.
    /// </summary>
    private static string ResolveLibraryConnectionString(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration.GetConnectionString("Library") ?? "Data Source=data/library.db";
        var builder = new SqliteConnectionStringBuilder(configured);

        if (!string.IsNullOrWhiteSpace(builder.DataSource) &&
            builder.DataSource != ":memory:" &&
            !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.Combine(environment.ContentRootPath, builder.DataSource);
        }

        return builder.ToString();
    }
}
