using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Doctors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Doctors.Commands.DeactivateDoctor;

/// <summary>
/// Desactiva un doctor; su usuario vinculado no cambia (un handler no modifica otro módulo).
/// </summary>
public sealed class DeactivateDoctorHandler(IApplicationDbContext db) : ICommandHandler<DeactivateDoctorCommand>
{
    public async Task<Unit> Handle(DeactivateDoctorCommand command, CancellationToken cancellationToken)
    {
        var doctorId = new DoctorId(command.DoctorId);

        var doctor = await db.Doctors.SingleOrDefaultAsync(d => d.Id == doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Doctor), command.DoctorId);

        // Sobre un doctor ya inactivo, el dominio lanza DomainException (400)
        doctor.Deactivate();
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
