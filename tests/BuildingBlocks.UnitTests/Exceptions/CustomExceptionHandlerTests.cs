using System.Text.Json;
using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Exceptions.Handler;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlocks.UnitTests.Exceptions;

public sealed class CustomExceptionHandlerTests
{
    private readonly CustomExceptionHandler _handler = new(NullLogger<CustomExceptionHandler>.Instance);

    [Fact]
    public async Task TryHandleAsync_WithDomainException_Returns400()
    {
        var (statusCode, problemDetails) = await HandleAsync(new DomainException("Rule broken"));

        statusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problemDetails.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problemDetails.Title.ShouldBe(nameof(DomainException));
    }

    [Fact]
    public async Task TryHandleAsync_WithBadHttpRequestException_ReturnsItsStatusCode()
    {
        var (statusCode, problemDetails) = await HandleAsync(new BadHttpRequestException("Failed to read the request body."));

        statusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problemDetails.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problemDetails.Title.ShouldBe(nameof(BadHttpRequestException));
    }

    [Fact]
    public async Task TryHandleAsync_WithPayloadTooLargeBadHttpRequestException_Returns413()
    {
        var (statusCode, _) = await HandleAsync(
            new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

        statusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);
    }

    [Fact]
    public async Task TryHandleAsync_WithUnauthorizedException_Returns401()
    {
        var (statusCode, problemDetails) = await HandleAsync(new UnauthorizedException("Invalid credentials"));

        statusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        problemDetails.Status.ShouldBe(StatusCodes.Status401Unauthorized);
        problemDetails.Title.ShouldBe(nameof(UnauthorizedException));
    }

    [Fact]
    public async Task TryHandleAsync_WithForbiddenException_Returns403()
    {
        var (statusCode, problemDetails) = await HandleAsync(new ForbiddenException("Access denied"));

        statusCode.ShouldBe(StatusCodes.Status403Forbidden);
        problemDetails.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problemDetails.Title.ShouldBe(nameof(ForbiddenException));
    }

    [Fact]
    public async Task TryHandleAsync_AnyException_WritesProblemJsonContentType()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await _handler.TryHandleAsync(context, new NotFoundException("Missing"), TestContext.Current.CancellationToken);

        context.Response.ContentType.ShouldStartWith("application/problem+json");
    }

    // Ejecuta el handler sobre un HttpContext en memoria y deserializa el ProblemDetails escrito en el body
    private async Task<(int StatusCode, ProblemDetails ProblemDetails)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await _handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.Body.Position = 0;
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body, JsonSerializerOptions.Web, TestContext.Current.CancellationToken);
        problemDetails.ShouldNotBeNull();
        return (context.Response.StatusCode, problemDetails);
    }
}
