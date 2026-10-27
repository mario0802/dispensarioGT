using Carter;
using Dispensario.Api.Endpoints.Users;
using Dispensario.Application.Users.Commands.RegisterUser;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Auth;

// Sin Role: el autoregistro siempre crea un Receptionist
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    DocumentType DocumentType,
    string DocumentNumber,
    string? Phone);

/// <summary>Autoregistro público de un recepcionista.</summary>
public sealed class RegisterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", async (RegisterRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new RegisterUserCommand(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.DocumentType,
                request.DocumentNumber,
                request.Phone,
                Role.Receptionist);
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/users/{result.Id}", result.Adapt<RegisterUserResponse>());
        })
        .WithName("Register")
        .WithTags("Auth")
        .WithSummary("Register as receptionist")
        .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict)
        .AllowAnonymous();
    }
}
