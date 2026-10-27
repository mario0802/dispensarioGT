using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Users.Commands.UpdateUserProfile;
using MediatR;

namespace Dispensario.Api.Endpoints.Users;

public sealed record UpdateUserProfileRequest(string FirstName, string LastName, string? Phone);

/// <summary>El Admin cambia el nombre y el teléfono de un usuario.</summary>
public sealed class UpdateUserProfileEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/{id:guid}", async (Guid id, UpdateUserProfileRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new UpdateUserProfileCommand(id, request.FirstName, request.LastName, request.Phone), ct);
            return Results.NoContent();
        })
        .WithName("UpdateUserProfile")
        .WithTags("Users")
        .WithSummary("Update user profile")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.AdminOnly);
    }
}
