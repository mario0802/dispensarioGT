using BuildingBlocks.Pagination;
using Carter;
using Dispensario.Application.Appointments.Queries.GetAppointments;
using Dispensario.Domain.Appointments;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Appointments;

// From y To son fechas locales del dispensario ("yyyy-MM-dd"), ambas inclusive
public sealed record GetAppointmentsRequest(
    int PageIndex = 0,
    int PageSize = 10,
    Guid? DoctorId = null,
    Guid? PatientId = null,
    AppointmentStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null);

/// <summary>Lista las citas paginadas y filtradas; un usuario Doctor solo ve las suyas.</summary>
public sealed class GetAppointmentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/appointments", async ([AsParameters] GetAppointmentsRequest request, ISender sender, CancellationToken ct) =>
        {
            var query = new GetAppointmentsQuery(
                new PaginationRequest(request.PageIndex, request.PageSize),
                request.DoctorId,
                request.PatientId,
                request.Status,
                request.From,
                request.To);
            var result = await sender.Send(query, ct);
            return Results.Ok(new PaginatedResult<AppointmentResponse>(
                result.PageIndex,
                result.PageSize,
                result.Count,
                result.Data.Adapt<List<AppointmentResponse>>()));
        })
        .WithName("GetAppointments")
        .WithTags("Appointments")
        .WithSummary("Get appointments")
        .Produces<PaginatedResult<AppointmentResponse>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .RequireAuthorization();
    }
}
