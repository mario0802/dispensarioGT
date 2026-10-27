using Carter;
using Dispensario.Application.Users.Commands.ChangePassword;
using MediatR;

namespace Dispensario.Api.Endpoints.Users;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>El usuario autenticado cambia su contraseña.</summary>
public sealed class ChangePasswordEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/me/password", async (ChangePasswordRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), ct);
            return Results.NoContent();
        })
        .WithName("ChangePassword")
        .WithTags("Users")
        .WithSummary("Change current user password")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
