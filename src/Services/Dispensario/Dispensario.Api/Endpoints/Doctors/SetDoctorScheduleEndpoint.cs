using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Doctors.Commands.SetDoctorSchedule;
using MediatR;

namespace Dispensario.Api.Endpoints.Doctors;

// Horas "HH:mm" en hora local del dispensario; la lista reemplaza todo el horario y vacía lo borra
public sealed record SetDoctorScheduleRequest(IReadOnlyList<ScheduleBlockRequest> Blocks);

public sealed record ScheduleBlockRequest(DayOfWeek DayOfWeek, string StartTime, string EndTime);

/// <summary>El Admin, o el propio doctor, reemplaza el horario semanal de un doctor.</summary>
public sealed class SetDoctorScheduleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/doctors/{id:guid}/schedule", async (Guid id, SetDoctorScheduleRequest request, ISender sender, CancellationToken ct) =>
        {
            // Blocks null llega al validador, que responde 400 con el error en Blocks
            var blocks = request.Blocks?
                .Select(block => block is null ? null! : new ScheduleBlockInput(block.DayOfWeek, block.StartTime, block.EndTime))
                .ToList();
            await sender.Send(new SetDoctorScheduleCommand(id, blocks!), ct);
            return Results.NoContent();
        })
        .WithName("SetDoctorSchedule")
        .WithTags("Doctors")
        .WithSummary("Set doctor weekly schedule")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.AdminOrDoctor);
    }
}
