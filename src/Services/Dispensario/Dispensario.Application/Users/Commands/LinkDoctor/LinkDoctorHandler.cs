using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Users.Commands.LinkDoctor;

/// <summary>
/// Vincula un usuario con rol Doctor con su ficha de doctor; lee Doctors pero solo escribe en Users.
/// </summary>
public sealed class LinkDoctorHandler(IApplicationDbContext db) : ICommandHandler<LinkDoctorCommand>
{
    public async Task<Unit> Handle(LinkDoctorCommand command, CancellationToken cancellationToken)
    {
        var userId = new UserId(command.UserId);
        var doctorId = new DoctorId(command.DoctorId);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), command.UserId);

        var doctorIsActive = await db.Doctors.AsNoTracking()
            .Where(d => d.Id == doctorId)
            .Select(d => (bool?)d.IsActive)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Doctor), command.DoctorId);

        if (!doctorIsActive)
            throw new BadRequestException("Doctor is inactive.");

        // El índice único de Users.DoctorId respalda esta regla ante peticiones simultáneas
        if (await db.Users.AnyAsync(u => u.Id != userId && u.DoctorId == doctorId, cancellationToken))
            throw new ConflictException($"Doctor {command.DoctorId} is already linked to another user.");

        // Rol distinto de Doctor o usuario ya vinculado → DomainException (400)
        user.LinkDoctor(doctorId);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
