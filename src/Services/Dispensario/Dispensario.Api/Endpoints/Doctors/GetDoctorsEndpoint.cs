using BuildingBlocks.Pagination;
using Carter;
using Dispensario.Application.Doctors.Queries.GetDoctors;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Doctors;

public sealed record GetDoctorsRequest(
    int PageIndex = 0,
    int PageSize = 10,
    string? Search = null,
    string? Specialty = null,
    bool? IsActive = null);

/// <summary>Lista los doctores paginados, con búsqueda y filtros por especialidad y estado.</summary>
public sealed class GetDoctorsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/doctors", async ([AsParameters] GetDoctorsRequest request, ISender sender, CancellationToken ct) =>
        {
            var query = new GetDoctorsQuery(
                new PaginationRequest(request.PageIndex, request.PageSize),
                request.Search,
                request.Specialty,
                request.IsActive);
            var result = await sender.Send(query, ct);
            return Results.Ok(new PaginatedResult<DoctorResponse>(
                result.PageIndex,
                result.PageSize,
                result.Count,
                result.Data.Adapt<List<DoctorResponse>>()));
        })
        .WithName("GetDoctors")
        .WithTags("Doctors")
        .WithSummary("Get doctors")
        .Produces<PaginatedResult<DoctorResponse>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
    }
}
