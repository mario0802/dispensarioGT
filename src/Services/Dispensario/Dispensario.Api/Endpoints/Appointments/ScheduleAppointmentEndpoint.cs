using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Appointments.Commands.ScheduleAppointment;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Appointments;

// StartLocal en hora local del dispensario y sin offset ("2026-11-03T09:30")
public sealed record ScheduleAppointmentRequest(Guid PatientId, Guid DoctorId, DateTime StartLocal, string Reason);

public sealed record ScheduleAppointmentResponse(Guid Id);

/// <summary>Recepción (o el Admin) agenda una cita en el horario del doctor.</summary>
public sealed class ScheduleAppointmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/appointments", async (ScheduleAppointmentRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(request.Adapt<ScheduleAppointmentCommand>(), ct);
            return Results.Created($"/api/appointments/{result.Id}", result.Adapt<ScheduleAppointmentResponse>());
        })
        .WithName("ScheduleAppointment")
        .WithTags("Appointments")
        .WithSummary("Schedule appointment")
        .Produces<ScheduleAppointmentResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(Policies.Reception);
    }
}
