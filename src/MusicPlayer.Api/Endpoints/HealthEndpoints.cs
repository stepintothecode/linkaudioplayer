using MusicPlayer.Abstractions;

namespace MusicPlayer.Api.Endpoints;

/// <summary>Liveness endpoint used by container health checks.</summary>
public static class HealthEndpoints
{
    /// <summary>Registers the health route.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", (IMediaCache cache) => Results.Ok(new
        {
            status = "ok",
            cacheBytes = cache.GetSizeBytes(),
        }))
        .WithTags("Health")
        .WithSummary("Reports that the server is up and how much audio is cached.");

        return app;
    }
}
