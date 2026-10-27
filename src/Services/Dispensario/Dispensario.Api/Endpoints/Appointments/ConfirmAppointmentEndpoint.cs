using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Appointments.Commands.ConfirmAppointment;
using MediatR;

namespace Dispensario.Api.Endpoints.Appointments;

/// <summary>Recepción (o el Admin) confirma una cita agendada antes de su inicio.</summary>
public sealed class ConfirmAppointmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/appointments/{id:guid}/confirm", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ConfirmAppointmentCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("ConfirmAppointment")
        .WithTags("Appointments")
        .WithSummary("Confirm appointment")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.Reception);
    }
}
