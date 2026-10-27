using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Doctors.Commands.UpdateDoctor;
using MediatR;

namespace Dispensario.Api.Endpoints.Doctors;

// Sin AppointmentDurationMinutes se conserva la duración actual; un phone null borra el teléfono
public sealed record UpdateDoctorRequest(
    string FirstName,
    string LastName,
    string LicenseNumber,
    string Specialty,
    string Email,
    string? Phone,
    int? AppointmentDurationMinutes);

/// <summary>El Admin edita los datos de un doctor.</summary>
public sealed class UpdateDoctorEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/doctors/{id:guid}", async (Guid id, UpdateDoctorRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateDoctorCommand(
                id,
                request.FirstName,
                request.LastName,
                request.LicenseNumber,
                request.Specialty,
                request.Email,
                request.Phone,
                request.AppointmentDurationMinutes);
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .WithName("UpdateDoctor")
        .WithTags("Doctors")
        .WithSummary("Update doctor")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(Policies.AdminOnly);
    }
}
