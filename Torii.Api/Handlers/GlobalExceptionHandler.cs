using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Torii.Api.Exceptions;

namespace Torii.Api.Handlers;

/// <summary>
/// Intercepte les exceptions non gérées et renvoie une réponse normalisée au
/// format <c>application/problem+json</c> (RFC 9457).
/// Les <see cref="ToriiException"/> sont mappées sur leur code HTTP et leur titre ;
/// toute autre exception devient une 500 générique, sans fuite de détails techniques.
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

        // Erreur métier attendue → warning ; erreur inattendue (500) → error avec stack.
        if (problemDetails.Status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Erreur non gérée : {Message}", exception.Message);
        else
            logger.LogWarning("Erreur métier ({Status}) : {Message}",
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

        return problem;
    }

    private static ProblemDetails Unexpected() => new()
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = "Erreur interne du serveur",
        Detail = "Une erreur inattendue est survenue.",
    };
}
