using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Users.Commands.UpdateUserProfile;

/// <summary>
/// El Admin cambia el nombre y el teléfono de un usuario.
/// </summary>
public sealed class UpdateUserProfileHandler(IApplicationDbContext db) : ICommandHandler<UpdateUserProfileCommand>
{
    public async Task<Unit> Handle(UpdateUserProfileCommand command, CancellationToken cancellationToken)
    {
        var userId = new UserId(command.UserId);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), command.UserId);

        user.UpdateProfile(
            FullName.Create(command.FirstName, command.LastName),
            command.Phone is null ? null : PhoneNumber.Create(command.Phone));
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
