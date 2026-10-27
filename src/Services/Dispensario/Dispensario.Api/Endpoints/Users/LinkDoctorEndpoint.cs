using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Users.Commands.LinkDoctor;
using MediatR;

namespace Dispensario.Api.Endpoints.Users;

public sealed record LinkDoctorRequest(Guid DoctorId);

/// <summary>El Admin vincula un usuario Doctor con su ficha; el claim doctor_id llega con el siguiente login.</summary>
public sealed class LinkDoctorEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/{id:guid}/link-doctor", async (Guid id, LinkDoctorRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new LinkDoctorCommand(id, request.DoctorId), ct);
            return Results.NoContent();
        })
        .WithName("LinkDoctor")
        .WithTags("Users")
        .WithSummary("Link user to doctor")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(Policies.AdminOnly);
    }
}
