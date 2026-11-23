using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Exceptions;

namespace Torii.Api.Handlers;

/// <summary>
/// Catches unhandled exceptions and returns a normalized
/// <c>application/problem+json</c> response (RFC 9457).
/// <see cref="ToriiException"/>s are mapped to their HTTP status code and title;
/// any other exception becomes a generic 500, without leaking technical details.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ToriiException ex => Business(ex),
            _ => Unexpected(),
        };

        // Expected business error → warning; unexpected error (500) → error with stack trace.
        if (problemDetails.Status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled error: {Message}", exception.Message);
        else
            logger.LogWarning("Business error ({Status}): {Message}",
                problemDetails.Status, exception.Message);

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    private static ProblemDetails Business(ToriiException ex)
    {
        var problem = new ProblemDetails
        {
            Status = ex.StatusCode,
            Title = ex.Title,
            Detail = ex.Message,
        };

        if (ex.Errors is not null)
            problem.Extensions["errors"] = ex.Errors;
        if (ex.Code is not null)
            problem.Extensions["code"] = ex.Code;

        return problem;
    }

    private static ProblemDetails Unexpected() => new()
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = "Internal server error",
        Detail = "An unexpected error occurred.",
    };
}
