using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Consultations.Queries.GetConsultationById;
using Dispensario.Domain.Consultations;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Consultations;

// StartedAtLocal y ClosedAtLocal salen sin offset (hora local del dispensario); StartedAtUtc y ClosedAtUtc con "Z"
public sealed record ConsultationResponse(
    Guid Id,
    Guid PatientId,
    string PatientFullName,
    Guid DoctorId,
    string DoctorFullName,
    Guid? AppointmentId,
    string Reason,
    string? Diagnosis,
    string? Notes,
    ConsultationStatus Status,
    DateTime StartedAtUtc,
    DateTime? ClosedAtUtc,
    DateTime StartedAtLocal,
    DateTime? ClosedAtLocal,
    DateTime CreatedAtUtc);

/// <summary>Devuelve una consulta por su ID; la leen el Admin y cualquier doctor vinculado.</summary>
public sealed class GetConsultationByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/consultations/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetConsultationByIdQuery(id), ct);
            return Results.Ok(result.Adapt<ConsultationResponse>());
        })
        .WithName("GetConsultationById")
        .WithTags("Consultations")
        .WithSummary("Get consultation by id")
        .Produces<ConsultationResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.AdminOrDoctor);
    }
}
