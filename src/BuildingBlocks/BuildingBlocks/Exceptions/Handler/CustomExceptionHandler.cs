using BuildingBlocks.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Exceptions.Handler;
public class CustomExceptionHandler
    (ILogger<CustomExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError("Error Message: {exceptionMessage}", exception.Message);

        (string Detail, string Title, int StatusCode) details = exception switch
        {
            InternalServerException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status500InternalServerError
            ),
            ValidationException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            BadRequestException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            // La lanza ASP.NET al no poder leer la petición (JSON mal formado, enum desconocido…); trae su propio código (400, 413…)
            BadHttpRequestException badHttpRequestException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = badHttpRequestException.StatusCode
            ),
            DomainException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            UnauthorizedException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status401Unauthorized
            ),
            ForbiddenException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status403Forbidden
            ),
            NotFoundException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status404NotFound
            ),
            ConflictException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status409Conflict
            ),
            _ =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status500InternalServerError
            )
        };

        var problemDetails = new ProblemDetails
        {
            Title = details.Title,
            Detail = details.Detail,
            Status = details.StatusCode,
            Instance = context.Request.Path
        };

        problemDetails.Extensions.Add("traceId", context.TraceIdentifier);

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions.Add("ValidationErrors", validationException.Errors);
        }

        // Sin contentType explícito WriteAsJsonAsync escribe application/json
        await context.Response.WriteAsJsonAsync(
            problemDetails, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}
