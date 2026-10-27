using Carter;
using Dispensario.Application.Patients.Queries.GetPatientByDocument;
using Dispensario.Domain.Common;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Patients;

// Query string: ?type=NationalId&number=0102030405
public sealed record GetPatientByDocumentRequest(DocumentType Type, string Number);

/// <summary>Busca un paciente por su documento exacto, también si está inactivo.</summary>
public sealed class GetPatientByDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Ruta literal: no choca con /api/patients/{id:guid}
        app.MapGet("/api/patients/by-document", async ([AsParameters] GetPatientByDocumentRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetPatientByDocumentQuery(request.Type, request.Number), ct);
            return Results.Ok(result.Adapt<PatientResponse>());
        })
        .WithName("GetPatientByDocument")
        .WithTags("Patients")
        .WithSummary("Get patient by document")
        .Produces<PatientResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
