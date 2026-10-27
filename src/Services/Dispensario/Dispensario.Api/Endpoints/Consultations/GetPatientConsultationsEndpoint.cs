using BuildingBlocks.Pagination;
using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Consultations.Queries.GetPatientConsultations;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Consultations;

public sealed record GetPatientConsultationsRequest(int PageIndex = 0, int PageSize = 10);

/// <summary>
/// Historia clínica paginada de un paciente. La ruta cuelga de /api/patients, pero la query es del módulo Consultations.
/// </summary>
public sealed class GetPatientConsultationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/patients/{id:guid}/consultations", async (
            Guid id,
            [AsParameters] GetPatientConsultationsRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetPatientConsultationsQuery(id, new PaginationRequest(request.PageIndex, request.PageSize));
            var result = await sender.Send(query, ct);
            return Results.Ok(new PaginatedResult<ConsultationResponse>(
                result.PageIndex,
                result.PageSize,
                result.Count,
                result.Data.Adapt<List<ConsultationResponse>>()));
        })
        .WithName("GetPatientConsultations")
        .WithTags("Consultations")
        .WithSummary("Get patient consultations")
        .Produces<PaginatedResult<ConsultationResponse>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.AdminOrDoctor);
    }
}
