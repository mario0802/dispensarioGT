using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Users.Dtos;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Users.Queries.GetUsers;

/// <summary>
/// Lista los usuarios paginados, con búsqueda de texto y filtros por rol y estado.
/// </summary>
public sealed class GetUsersHandler(IApplicationDbContext db) : IQueryHandler<GetUsersQuery, PaginatedResult<UserDto>>
{
    public const int MaxPageSize = 100;

    private const string LikeEscape = "\\";

    public async Task<PaginatedResult<UserDto>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        var (pageIndex, pageSize) = query.Pagination;

        // Las queries no pasan por ValidationBehavior: la paginación se valida aquí
        if (pageIndex < 0)
            throw new BadRequestException("PageIndex must be greater than or equal to 0.");

        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"PageSize must be between 1 and {MaxPageSize}.");

        var source = db.Users.AsNoTracking();

        if (query.Role is { } role)
            source = source.Where(u => u.Role == role);

        if (query.IsActive is { } isActive)
            source = source.Where(u => u.IsActive == isActive);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // LIKE en SQLite no distingue mayúsculas (en ASCII); instr, que genera Contains, sí
            var pattern = $"%{EscapeLike(search)}%";

            // El doble cast lleva a la columna Email sin pasar por el converter del VO
            source = source.Where(u =>
                EF.Functions.Like(u.FullName.FirstName, pattern, LikeEscape) ||
                EF.Functions.Like(u.FullName.LastName, pattern, LikeEscape) ||
                EF.Functions.Like((string)(object)u.Email, pattern, LikeEscape) ||
                EF.Functions.Like(u.Identification.Number, pattern, LikeEscape));
        }

        var count = await source.LongCountAsync(cancellationToken);

        var users = await source
            .OrderBy(u => u.FullName.LastName)
            .ThenBy(u => u.FullName.FirstName)
            .ThenBy(u => u.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ProjectToType<UserDto>()
            .ToListAsync(cancellationToken);

        return new PaginatedResult<UserDto>(pageIndex, pageSize, count, users);
    }

    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape).Replace("%", LikeEscape + "%").Replace("_", LikeEscape + "_");
}
