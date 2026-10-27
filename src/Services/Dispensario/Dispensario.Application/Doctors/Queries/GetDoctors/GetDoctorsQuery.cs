using BuildingBlocks.CQRS;
using BuildingBlocks.Pagination;
using Dispensario.Application.Doctors.Dtos;

namespace Dispensario.Application.Doctors.Queries.GetDoctors;

public sealed record GetDoctorsQuery(PaginationRequest Pagination, string? Search, string? Specialty, bool? IsActive)
    : IQuery<PaginatedResult<DoctorDto>>;
