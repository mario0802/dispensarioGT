using Carter;
using Dispensario.Application.Doctors.Queries.GetDoctorSchedule;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Doctors;

public sealed record DoctorScheduleResponse(Guid DoctorId, IReadOnlyList<ScheduleBlockResponse> Blocks);

public sealed record ScheduleBlockResponse(DayOfWeek DayOfWeek, string StartTime, string EndTime);

/// <summary>Devuelve el horario semanal de un doctor, de lunes a domingo.</summary>
public sealed class GetDoctorScheduleEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/doctors/{id:guid}/schedule", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDoctorScheduleQuery(id), ct);
            return Results.Ok(result.Adapt<DoctorScheduleResponse>());
        })
        .WithName("GetDoctorSchedule")
        .WithTags("Doctors")
        .WithSummary("Get doctor weekly schedule")
        .Produces<DoctorScheduleResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
