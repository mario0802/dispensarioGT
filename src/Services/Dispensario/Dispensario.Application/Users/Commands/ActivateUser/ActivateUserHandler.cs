using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Users.Commands.ActivateUser;

/// <summary>
/// Vuelve a activar un usuario desactivado.
/// </summary>
public sealed class ActivateUserHandler(IApplicationDbContext db) : ICommandHandler<ActivateUserCommand>
{
    public async Task<Unit> Handle(ActivateUserCommand command, CancellationToken cancellationToken)
    {
        var userId = new UserId(command.UserId);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), command.UserId);

        // Sobre un usuario ya activo, el dominio lanza DomainException (400)
        user.Activate();
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
