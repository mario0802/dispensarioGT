using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Consultations.Commands.StartConsultation;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Consultations;

// AppointmentId es opcional: sin cita, la consulta es de un paciente sin agendar
public sealed record StartConsultationRequest(Guid PatientId, Guid? AppointmentId, string Reason);

public sealed record StartConsultationResponse(Guid Id);

/// <summary>El doctor actual inicia una consulta, con o sin cita.</summary>
public sealed class StartConsultationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/consultations", async (StartConsultationRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(request.Adapt<StartConsultationCommand>(), ct);
            return Results.Created($"/api/consultations/{result.Id}", result.Adapt<StartConsultationResponse>());
        })
        .WithName("StartConsultation")
        .WithTags("Consultations")
        .WithSummary("Start consultation")
        .Produces<StartConsultationResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(Policies.Clinical);
    }
}
