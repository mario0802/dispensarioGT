using Carter;
using Dispensario.Application.Appointments.Queries.GetAvailableSlots;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Appointments;

public sealed record AvailableSlotsResponse(
    Guid DoctorId,
    DateOnly Date,
    int DurationMinutes,
    IReadOnlyList<AvailableSlotResponse> Slots);

// Horas locales del dispensario, sin offset
public sealed record AvailableSlotResponse(DateTime StartLocal, DateTime EndLocal);

/// <summary>
/// Devuelve los slots libres de un doctor en una fecha local. La ruta cuelga de /api/doctors,
/// pero la query es del módulo Appointments.
/// </summary>
public sealed class GetAvailableSlotsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/doctors/{id:guid}/available-slots", async (Guid id, DateOnly date, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAvailableSlotsQuery(id, date), ct);
            return Results.Ok(result.Adapt<AvailableSlotsResponse>());
        })
        .WithName("GetAvailableSlots")
        .WithTags("Appointments")
        .WithSummary("Get doctor available slots")
        .Produces<AvailableSlotsResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
