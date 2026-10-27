using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Common;
using Dispensario.Application.Patients.Dtos;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Patients.Queries.GetPatients;

/// <summary>
/// Lista los pacientes paginados, con búsqueda de texto y filtro por estado.
/// </summary>
public sealed class GetPatientsHandler(IApplicationDbContext db) : IQueryHandler<GetPatientsQuery, PaginatedResult<PatientDto>>
{
    public const int MaxPageSize = 100;

    public async Task<PaginatedResult<PatientDto>> Handle(GetPatientsQuery query, CancellationToken cancellationToken)
    {
        var (pageIndex, pageSize) = query.Pagination;

        // Las queries no pasan por ValidationBehavior: la paginación se valida aquí
        if (pageIndex < 0)
            throw new BadRequestException("PageIndex must be greater than or equal to 0.");

        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"PageSize must be between 1 and {MaxPageSize}.");

        var source = db.Patients.AsNoTracking();

        if (query.IsActive is { } isActive)
            source = source.Where(p => p.IsActive == isActive);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // LIKE en SQLite no distingue mayúsculas (en ASCII); instr, que genera Contains, sí
            var pattern = LikePatterns.Contains(search);
            source = source.Where(p =>
                EF.Functions.Like(p.FullName.FirstName, pattern, LikePatterns.Escape) ||
                EF.Functions.Like(p.FullName.LastName, pattern, LikePatterns.Escape) ||
                EF.Functions.Like(p.Document.Number, pattern, LikePatterns.Escape));
        }

        var count = await source.LongCountAsync(cancellationToken);

        var patients = await source
            .OrderBy(p => p.FullName.LastName)
            .ThenBy(p => p.FullName.FirstName)
            .ThenBy(p => p.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ProjectToType<PatientDto>()
            .ToListAsync(cancellationToken);

        return new PaginatedResult<PatientDto>(pageIndex, pageSize, count, patients);
    }
}
