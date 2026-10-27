using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Users.Commands.UpdateCurrentUserProfile;

/// <summary>
/// El usuario autenticado cambia su nombre y su teléfono.
/// </summary>
public sealed class UpdateCurrentUserProfileHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : ICommandHandler<UpdateCurrentUserProfileCommand>
{
    public async Task<Unit> Handle(UpdateCurrentUserProfileCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("User is not authenticated.");

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId.Value);

        // El token sigue siendo válido tras desactivar al usuario; aquí se rechaza, igual que en /me
        if (!user.IsActive)
            throw new UnauthorizedException("User is inactive.");

        user.UpdateProfile(
            FullName.Create(command.FirstName, command.LastName),
            command.Phone is null ? null : PhoneNumber.Create(command.Phone));
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
