using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Users.Dtos;
using Dispensario.Domain.Users;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Users.Queries.GetUserById;

/// <summary>
/// Devuelve un usuario por su ID.
/// </summary>
public sealed class GetUserByIdHandler(IApplicationDbContext db) : IQueryHandler<GetUserByIdQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        var userId = new UserId(query.UserId);

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), query.UserId);

        return user.Adapt<UserDto>();
    }
}
