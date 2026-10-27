using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Users.Commands.ResetUserPassword;
using MediatR;

namespace Dispensario.Api.Endpoints.Users;

public sealed record ResetUserPasswordRequest(string NewPassword);

/// <summary>El Admin fija una contraseña nueva a otro usuario.</summary>
public sealed class ResetUserPasswordEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/{id:guid}/password", async (Guid id, ResetUserPasswordRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ResetUserPasswordCommand(id, request.NewPassword), ct);
            return Results.NoContent();
        })
        .WithName("ResetUserPassword")
        .WithTags("Users")
        .WithSummary("Reset user password")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.AdminOnly);
    }
}
