using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Patients.Commands.ActivatePatient;
using MediatR;

namespace Dispensario.Api.Endpoints.Patients;

/// <summary>Vuelve a activar un paciente desactivado.</summary>
public sealed class ActivatePatientEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/patients/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ActivatePatientCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("ActivatePatient")
        .WithTags("Patients")
        .WithSummary("Activate patient")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.Reception);
    }
}
