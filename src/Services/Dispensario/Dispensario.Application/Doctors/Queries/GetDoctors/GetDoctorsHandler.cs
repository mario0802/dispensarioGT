using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Common;
using Dispensario.Application.Doctors.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Doctors.Queries.GetDoctors;

/// <summary>
/// Lista los doctores paginados, con búsqueda de texto y filtros por especialidad y estado.
/// </summary>
public sealed class GetDoctorsHandler(IApplicationDbContext db) : IQueryHandler<GetDoctorsQuery, PaginatedResult<DoctorDto>>
{
    public const int MaxPageSize = 100;

    public async Task<PaginatedResult<DoctorDto>> Handle(GetDoctorsQuery query, CancellationToken cancellationToken)
    {
        var (pageIndex, pageSize) = query.Pagination;

        // Las queries no pasan por ValidationBehavior: la paginación se valida aquí
        if (pageIndex < 0)
            throw new BadRequestException("PageIndex must be greater than or equal to 0.");

        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"PageSize must be between 1 and {MaxPageSize}.");

        var source = db.Doctors.AsNoTracking();

        if (query.IsActive is { } isActive)
            source = source.Where(d => d.IsActive == isActive);

        // LIKE sin comodines: igualdad sin distinguir mayúsculas (en ASCII)
        var specialty = query.Specialty?.Trim();
        if (!string.IsNullOrEmpty(specialty))
        {
            var pattern = LikePatterns.Equal(specialty);
            source = source.Where(d => EF.Functions.Like(d.Specialty, pattern, LikePatterns.Escape));
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = LikePatterns.Contains(search);
            source = source.Where(d =>
                EF.Functions.Like(d.FullName.FirstName, pattern, LikePatterns.Escape) ||
                EF.Functions.Like(d.FullName.LastName, pattern, LikePatterns.Escape) ||
                EF.Functions.Like(d.LicenseNumber, pattern, LikePatterns.Escape) ||
                EF.Functions.Like(d.Specialty, pattern, LikePatterns.Escape));
        }

        var count = await source.LongCountAsync(cancellationToken);

        var doctors = await source
            .OrderBy(d => d.FullName.LastName)
            .ThenBy(d => d.FullName.FirstName)
            .ThenBy(d => d.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToDto(db.Users.AsNoTracking())
            .ToListAsync(cancellationToken);

        return new PaginatedResult<DoctorDto>(pageIndex, pageSize, count, doctors);
    }
}
