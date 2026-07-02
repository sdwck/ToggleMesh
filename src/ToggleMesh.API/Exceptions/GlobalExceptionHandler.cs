using Microsoft.AspNetCore.Diagnostics;

namespace ToggleMesh.API.Exceptions;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception");

        var response = exception switch
        {
            _ => new
            {
                status = 500,
                error = "Internal Server Error",
                message = "An unexpected error occurred."
            }
        };

        httpContext.Response.StatusCode = 500;

        await httpContext.Response.WriteAsJsonAsync(
            response,
            cancellationToken);

        return true;
    }
}