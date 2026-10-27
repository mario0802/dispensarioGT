using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Patients.Commands.CreatePatient;
using Dispensario.Application.Patients.Commands.UpdatePatient;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;
using MediatR;

namespace Dispensario.Api.Endpoints.Patients;

// Reemplaza todos los datos: un opcional null (email, phone, address, bloodType, allergies) borra el valor actual
public sealed record UpdatePatientRequest(
    DocumentType DocumentType,
    string DocumentNumber,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    Gender Gender,
    string? Email,
    string? Phone,
    AddressRequest? Address,
    string? BloodType,
    string? Allergies);

/// <summary>Recepción (o el Admin) edita los datos de un paciente, incluido su documento.</summary>
public sealed class UpdatePatientEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/patients/{id:guid}", async (Guid id, UpdatePatientRequest request, ISender sender, CancellationToken ct) =>
        {
            var address = request.Address is null
                ? null
                : new AddressInput(request.Address.Street, request.Address.City, request.Address.State, request.Address.Reference);
            var command = new UpdatePatientCommand(
                id,
                request.DocumentType,
                request.DocumentNumber,
                request.FirstName,
                request.LastName,
                request.BirthDate,
                request.Gender,
                request.Email,
                request.Phone,
                address,
                request.BloodType,
                request.Allergies);
            await sender.Send(command, ct);
            return Results.NoContent();
        })
        .WithName("UpdatePatient")
        .WithTags("Patients")
        .WithSummary("Update patient")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(Policies.Reception);
    }
}
