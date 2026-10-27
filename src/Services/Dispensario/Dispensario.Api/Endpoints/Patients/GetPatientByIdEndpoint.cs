using Carter;
using Dispensario.Application.Patients.Queries.GetPatientById;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Patients;

// BloodType en notación clínica ("A+"); Address null si el paciente no tiene dirección
public sealed record PatientResponse(
    Guid Id,
    DocumentType DocumentType,
    string DocumentNumber,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    Gender Gender,
    string? Email,
    string? Phone,
    AddressResponse? Address,
    string? BloodType,
    string? Allergies,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record AddressResponse(string Street, string City, string? State, string? Reference);

/// <summary>Devuelve un paciente por su ID.</summary>
public sealed class GetPatientByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/patients/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetPatientByIdQuery(id), ct);
            return Results.Ok(result.Adapt<PatientResponse>());
        })
        .WithName("GetPatientById")
        .WithTags("Patients")
        .WithSummary("Get patient by id")
        .Produces<PatientResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
