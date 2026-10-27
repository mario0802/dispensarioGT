using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Patients.Commands.CreatePatient;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Patients;

// BloodType en notación clínica ("A+", "O-"…); los opcionales se pueden omitir o enviar como null
public sealed record CreatePatientRequest(
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

public sealed record AddressRequest(string Street, string City, string? State, string? Reference);

public sealed record CreatePatientResponse(Guid Id);

/// <summary>Recepción (o el Admin) registra un paciente.</summary>
public sealed class CreatePatientEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/patients", async (CreatePatientRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(request.Adapt<CreatePatientCommand>(), ct);
            return Results.Created($"/api/patients/{result.Id}", result.Adapt<CreatePatientResponse>());
        })
        .WithName("CreatePatient")
        .WithTags("Patients")
        .WithSummary("Create patient")
        .Produces<CreatePatientResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .RequireAuthorization(Policies.Reception);
    }
}
