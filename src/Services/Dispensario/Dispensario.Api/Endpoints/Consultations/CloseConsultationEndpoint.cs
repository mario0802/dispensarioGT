using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Consultations.Commands.CloseConsultation;
using MediatR;

namespace Dispensario.Api.Endpoints.Consultations;

public sealed record CloseConsultationRequest(string Diagnosis, string? Notes);

/// <summary>El doctor dueño cierra una consulta abierta con su diagnóstico.</summary>
public sealed class CloseConsultationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/consultations/{id:guid}/close", async (Guid id, CloseConsultationRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CloseConsultationCommand(id, request.Diagnosis, request.Notes), ct);
            return Results.NoContent();
        })
        .WithName("CloseConsultation")
        .WithTags("Consultations")
        .WithSummary("Close consultation")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.Clinical);
    }
}
