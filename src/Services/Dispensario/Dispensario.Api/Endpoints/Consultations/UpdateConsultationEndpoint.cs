using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Consultations.Commands.UpdateConsultation;
using MediatR;

namespace Dispensario.Api.Endpoints.Consultations;

// Reemplaza los tres textos: un opcional null borra el valor
public sealed record UpdateConsultationRequest(string Reason, string? Diagnosis, string? Notes);

/// <summary>El doctor dueño edita una consulta abierta.</summary>
public sealed class UpdateConsultationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/consultations/{id:guid}", async (Guid id, UpdateConsultationRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new UpdateConsultationCommand(id, request.Reason, request.Diagnosis, request.Notes), ct);
            return Results.NoContent();
        })
        .WithName("UpdateConsultation")
        .WithTags("Consultations")
        .WithSummary("Update consultation")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.Clinical);
    }
}
