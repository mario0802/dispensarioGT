using Carter;
using Dispensario.Application.Doctors.Queries.GetDoctorById;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Doctors;

public sealed record DoctorResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string LicenseNumber,
    string Specialty,
    string Email,
    string? Phone,
    int AppointmentDurationMinutes,
    bool IsActive,
    Guid? UserId,
    DateTime CreatedAtUtc);

/// <summary>Devuelve un doctor por su ID.</summary>
public sealed class GetDoctorByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/doctors/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDoctorByIdQuery(id), ct);
            return Results.Ok(result.Adapt<DoctorResponse>());
        })
        .WithName("GetDoctorById")
        .WithTags("Doctors")
        .WithSummary("Get doctor by id")
        .Produces<DoctorResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
