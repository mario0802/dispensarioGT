using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Users.Dtos;
using Dispensario.Domain.Users;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Users.Queries.GetCurrentUser;

/// <summary>
/// Devuelve los datos actuales del usuario del token, leídos de la base de datos.
/// </summary>
public sealed class GetCurrentUserHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserDto>
{
    public async Task<CurrentUserDto> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("User is not authenticated.");

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId.Value);

        // El token sigue siendo válido tras desactivar al usuario; /me sí lo rechaza
        if (!user.IsActive)
            throw new UnauthorizedException("User is inactive.");

        return user.Adapt<CurrentUserDto>();
    }
}
