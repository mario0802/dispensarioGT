using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Doctors.Commands.CreateDoctor;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Doctors;

// Sin AppointmentDurationMinutes, el doctor queda con 30 minutos por cita
public sealed record CreateDoctorRequest(
    string FirstName,
    string LastName,
    string LicenseNumber,
    string Specialty,
    string Email,
    string? Phone,
    int? AppointmentDurationMinutes);

public sealed record CreateDoctorResponse(Guid Id);

/// <summary>El Admin registra un doctor.</summary>
public sealed class CreateDoctorEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/doctors", async (CreateDoctorRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(request.Adapt<CreateDoctorCommand>(), ct);
            return Results.Created($"/api/doctors/{result.Id}", result.Adapt<CreateDoctorResponse>());
        })
        .WithName("CreateDoctor")
        .WithTags("Doctors")
        .WithSummary("Create doctor")
        .Produces<CreateDoctorResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(Policies.AdminOnly);
    }
}
