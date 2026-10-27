using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Doctors.Commands.ActivateDoctor;
using MediatR;

namespace Dispensario.Api.Endpoints.Doctors;

/// <summary>Vuelve a activar un doctor desactivado.</summary>
public sealed class ActivateDoctorEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/doctors/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ActivateDoctorCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("ActivateDoctor")
        .WithTags("Doctors")
        .WithSummary("Activate doctor")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.AdminOnly);
    }
}
