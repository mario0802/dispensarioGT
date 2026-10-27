using Carter;
using Dispensario.Application.Appointments.Commands.MarkAppointmentNoShow;
using MediatR;

namespace Dispensario.Api.Endpoints.Appointments;

/// <summary>Marca como no asistida una cita ya empezada; un usuario Doctor solo puede marcar las suyas.</summary>
public sealed class MarkAppointmentNoShowEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/appointments/{id:guid}/no-show", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new MarkAppointmentNoShowCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("MarkAppointmentNoShow")
        .WithTags("Appointments")
        .WithSummary("Mark appointment as no-show")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
