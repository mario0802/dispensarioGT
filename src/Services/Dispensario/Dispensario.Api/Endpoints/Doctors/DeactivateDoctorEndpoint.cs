using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Doctors.Commands.DeactivateDoctor;
using MediatR;

namespace Dispensario.Api.Endpoints.Doctors;

/// <summary>Desactiva un doctor; su usuario vinculado no cambia.</summary>
public sealed class DeactivateDoctorEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/doctors/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeactivateDoctorCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("DeactivateDoctor")
        .WithTags("Doctors")
        .WithSummary("Deactivate doctor")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.AdminOnly);
    }
}
