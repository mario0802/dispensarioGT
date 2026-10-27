using Carter;
using Dispensario.Application.Appointments.Queries.GetAppointmentById;
using Dispensario.Domain.Appointments;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Appointments;

// StartLocal y EndLocal salen sin offset (hora local del dispensario); StartUtc y EndUtc con "Z"
public sealed record AppointmentResponse(
    Guid Id,
    Guid PatientId,
    string PatientFullName,
    Guid DoctorId,
    string DoctorFullName,
    DateTime StartUtc,
    DateTime EndUtc,
    DateTime StartLocal,
    DateTime EndLocal,
    string Reason,
    AppointmentStatus Status,
    string? CancellationReason,
    DateTime CreatedAtUtc);

/// <summary>Devuelve una cita por su ID; un usuario Doctor solo ve las suyas.</summary>
public sealed class GetAppointmentByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/appointments/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAppointmentByIdQuery(id), ct);
            return Results.Ok(result.Adapt<AppointmentResponse>());
        })
        .WithName("GetAppointmentById")
        .WithTags("Appointments")
        .WithSummary("Get appointment by id")
        .Produces<AppointmentResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
