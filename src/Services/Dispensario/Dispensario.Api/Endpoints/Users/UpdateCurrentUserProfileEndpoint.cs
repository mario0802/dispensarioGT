using Carter;
using Dispensario.Application.Users.Commands.UpdateCurrentUserProfile;
using MediatR;

namespace Dispensario.Api.Endpoints.Users;

public sealed record UpdateCurrentUserProfileRequest(string FirstName, string LastName, string? Phone);

/// <summary>El usuario autenticado cambia su nombre y su teléfono.</summary>
public sealed class UpdateCurrentUserProfileEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/me", async (UpdateCurrentUserProfileRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new UpdateCurrentUserProfileCommand(request.FirstName, request.LastName, request.Phone), ct);
            return Results.NoContent();
        })
        .WithName("UpdateCurrentUserProfile")
        .WithTags("Users")
        .WithSummary("Update current user profile")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
