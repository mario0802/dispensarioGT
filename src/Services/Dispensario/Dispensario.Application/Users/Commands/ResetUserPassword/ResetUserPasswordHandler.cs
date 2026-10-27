using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Users.Commands.ResetUserPassword;

/// <summary>
/// El Admin resetea la contraseña de otro usuario, también si está inactivo.
/// </summary>
public sealed class ResetUserPasswordHandler(IApplicationDbContext db, IPasswordHasher passwordHasher, ICurrentUser currentUser)
    : ICommandHandler<ResetUserPasswordCommand>
{
    public async Task<Unit> Handle(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        var userId = new UserId(command.UserId);

        // Para la propia contraseña se exige la actual: un token robado no basta para cambiarla
        if (userId == currentUser.UserId)
            throw new BadRequestException("Use /api/users/me/password to change your own password.");

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), command.UserId);

        user.ChangePassword(passwordHasher.Hash(command.NewPassword));
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
