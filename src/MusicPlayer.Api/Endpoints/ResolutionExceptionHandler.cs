using Microsoft.AspNetCore.Diagnostics;
using MusicPlayer.Abstractions;

namespace MusicPlayer.Api.Endpoints;

/// <summary>Turns resolution failures into problem responses instead of 500s.</summary>
public sealed class ResolutionExceptionHandler : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not MediaResolutionException resolution)
        {
            return false;
        }

        // A DRM refusal is a deliberate policy decision, so it reads as 403 rather than a failure.
        var status = resolution.Verdict == SourceVerdict.BlockedDrm
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status400BadRequest;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new { error = resolution.Message, verdict = resolution.Verdict.ToString() },
            cancellationToken);

        return true;
    }
}
