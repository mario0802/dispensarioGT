using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Patients.Commands.DeactivatePatient;
using MediatR;

namespace Dispensario.Api.Endpoints.Patients;

/// <summary>Desactiva un paciente.</summary>
public sealed class DeactivatePatientEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/patients/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeactivatePatientCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("DeactivatePatient")
        .WithTags("Patients")
        .WithSummary("Deactivate patient")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.Reception);
    }
}
