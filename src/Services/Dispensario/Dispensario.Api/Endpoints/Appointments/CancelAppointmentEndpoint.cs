using Carter;
using Dispensario.Application.Appointments.Commands.CancelAppointment;
using MediatR;

namespace Dispensario.Api.Endpoints.Appointments;

public sealed record CancelAppointmentRequest(string Reason);

/// <summary>Cancela una cita antes de su inicio; un usuario Doctor solo puede cancelar las suyas.</summary>
public sealed class CancelAppointmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/appointments/{id:guid}/cancel", async (Guid id, CancelAppointmentRequest request, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CancelAppointmentCommand(id, request.Reason), ct);
            return Results.NoContent();
        })
        .WithName("CancelAppointment")
        .WithTags("Appointments")
        .WithSummary("Cancel appointment")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
