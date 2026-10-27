using BuildingBlocks.Pagination;
using Carter;
using Dispensario.Application.Patients.Queries.GetPatients;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Patients;

public sealed record GetPatientsRequest(
    int PageIndex = 0,
    int PageSize = 10,
    string? Search = null,
    bool? IsActive = null);

/// <summary>Lista los pacientes paginados, con búsqueda por nombre o documento y filtro por estado.</summary>
public sealed class GetPatientsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/patients", async ([AsParameters] GetPatientsRequest request, ISender sender, CancellationToken ct) =>
        {
            var query = new GetPatientsQuery(
                new PaginationRequest(request.PageIndex, request.PageSize),
                request.Search,
                request.IsActive);
            var result = await sender.Send(query, ct);
            return Results.Ok(new PaginatedResult<PatientResponse>(
                result.PageIndex,
                result.PageSize,
                result.Count,
                result.Data.Adapt<List<PatientResponse>>()));
        })
        .WithName("GetPatients")
        .WithTags("Patients")
        .WithSummary("Get patients")
        .Produces<PaginatedResult<PatientResponse>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
    }
}
