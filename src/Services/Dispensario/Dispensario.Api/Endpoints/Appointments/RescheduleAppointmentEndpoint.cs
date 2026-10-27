using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Appointments.Commands.RescheduleAppointment;
using MediatR;

namespace Dispensario.Api.Endpoints.Appointments;

// StartLocal en hora local del dispensario y sin offset ("2026-11-04T10:00")
public sealed record RescheduleAppointmentRequest(DateTime StartLocal);

/// <summary>Recepción (o el Admin) mueve una cita a otra hora con el mismo doctor.</summary>
public sealed class RescheduleAppointmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/appointments/{id:guid}/schedule", async (Guid id, RescheduleAppointmentRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new RescheduleAppointmentCommand(id, request.StartLocal), ct);
            return Results.NoContent();
        })
        .WithName("RescheduleAppointment")
        .WithTags("Appointments")
        .WithSummary("Reschedule appointment")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(Policies.Reception);
    }
}
