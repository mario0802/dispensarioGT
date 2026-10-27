using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Users.Commands.ActivateUser;
using MediatR;

namespace Dispensario.Api.Endpoints.Users;

/// <summary>Vuelve a activar un usuario desactivado.</summary>
public sealed class ActivateUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ActivateUserCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("ActivateUser")
        .WithTags("Users")
        .WithSummary("Activate user")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.AdminOnly);
    }
}
