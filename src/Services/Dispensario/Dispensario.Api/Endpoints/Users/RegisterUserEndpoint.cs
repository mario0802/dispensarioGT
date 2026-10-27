using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Users.Commands.RegisterUser;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Users;

// Role es nullable: sin él, el valor por defecto del enum (Admin) crearía un Admin sin pedirlo
public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    DocumentType DocumentType,
    string DocumentNumber,
    string? Phone,
    Role? Role);

public sealed record RegisterUserResponse(Guid Id);

/// <summary>El Admin registra un usuario de cualquier rol.</summary>
public sealed class RegisterUserEndpoint : ICarterModule
{
    // Valor fuera del enum: el validador lo rechaza con un error en Role
    private const Role MissingRole = (Role)(-1);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users", async (RegisterUserRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new RegisterUserCommand(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.DocumentType,
                request.DocumentNumber,
                request.Phone,
                request.Role ?? MissingRole);
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/users/{result.Id}", result.Adapt<RegisterUserResponse>());
        })
        .WithName("RegisterUser")
        .WithTags("Users")
        .WithSummary("Register user")
        .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(Policies.AdminOnly);
    }
}
