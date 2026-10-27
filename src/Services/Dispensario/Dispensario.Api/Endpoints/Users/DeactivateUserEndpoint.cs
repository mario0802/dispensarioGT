using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Users.Commands.DeactivateUser;
using MediatR;

namespace Dispensario.Api.Endpoints.Users;

/// <summary>Desactiva un usuario; ya no puede iniciar sesión.</summary>
public sealed class DeactivateUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeactivateUserCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("DeactivateUser")
        .WithTags("Users")
        .WithSummary("Deactivate user")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(Policies.AdminOnly);
    }
}
