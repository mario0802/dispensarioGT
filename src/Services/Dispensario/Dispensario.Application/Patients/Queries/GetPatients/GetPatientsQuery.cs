using BuildingBlocks.CQRS;
using BuildingBlocks.Pagination;
using Dispensario.Application.Patients.Dtos;

namespace Dispensario.Application.Patients.Queries.GetPatients;

public sealed record GetPatientsQuery(PaginationRequest Pagination, string? Search, bool? IsActive)
    : IQuery<PaginatedResult<PatientDto>>;
