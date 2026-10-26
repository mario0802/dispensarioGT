using Carter;
using Dispensario.Application.Users.Commands.Login;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc);

/// <summary>Autentica con email y contraseña y devuelve un JWT de acceso.</summary>
public sealed class LoginEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (LoginRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(request.Adapt<LoginCommand>(), ct);
            return Results.Ok(result.Adapt<LoginResponse>());
        })
        .WithName("Login")
        .WithTags("Auth")
        .WithSummary("Log in")
        .Produces<LoginResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .AllowAnonymous();
    }
}
