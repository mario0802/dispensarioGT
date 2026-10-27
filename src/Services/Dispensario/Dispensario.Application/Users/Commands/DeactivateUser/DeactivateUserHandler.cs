using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Users.Commands.DeactivateUser;

/// <summary>
/// Desactiva un usuario; nadie se desactiva a sí mismo y siempre queda un Admin activo.
/// </summary>
public sealed class DeactivateUserHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : ICommandHandler<DeactivateUserCommand>
{
    public async Task<Unit> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        var userId = new UserId(command.UserId);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), command.UserId);

        if (user.Id == currentUser.UserId)
            throw new BadRequestException("You cannot deactivate your own user.");

        // Necesita contar en la BD, así que la regla vive aquí y no en el dominio
        if (user is { Role: Role.Admin, IsActive: true }
            && await db.Users.CountAsync(u => u.Role == Role.Admin && u.IsActive, cancellationToken) == 1)
            throw new ConflictException("Cannot deactivate the last active admin.");

        // Sobre un usuario ya inactivo, el dominio lanza DomainException (400)
        user.Deactivate();
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
