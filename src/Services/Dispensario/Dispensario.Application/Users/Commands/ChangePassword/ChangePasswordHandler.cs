using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Users.Commands.ChangePassword;

/// <summary>
/// El usuario autenticado cambia su contraseña; exige la actual.
/// </summary>
public sealed class ChangePasswordHandler(IApplicationDbContext db, IPasswordHasher passwordHasher, ICurrentUser currentUser)
    : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Unit> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("User is not authenticated.");

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId.Value);

        // El token sigue siendo válido tras desactivar al usuario; aquí se rechaza, igual que en /me
        if (!user.IsActive)
            throw new UnauthorizedException("User is inactive.");

        // 400 y no 401: el token es válido, lo que falla es el dato enviado
        if (!passwordHasher.Verify(user.PasswordHash, command.CurrentPassword))
            throw new BadRequestException("Current password is incorrect.");

        user.ChangePassword(passwordHasher.Hash(command.NewPassword));
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
