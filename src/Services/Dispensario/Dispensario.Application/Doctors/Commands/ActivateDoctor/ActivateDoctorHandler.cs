using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Doctors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Doctors.Commands.ActivateDoctor;

/// <summary>
/// Vuelve a activar un doctor desactivado.
/// </summary>
public sealed class ActivateDoctorHandler(IApplicationDbContext db) : ICommandHandler<ActivateDoctorCommand>
{
    public async Task<Unit> Handle(ActivateDoctorCommand command, CancellationToken cancellationToken)
    {
        var doctorId = new DoctorId(command.DoctorId);

        var doctor = await db.Doctors.SingleOrDefaultAsync(d => d.Id == doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Doctor), command.DoctorId);

        // Sobre un doctor ya activo, el dominio lanza DomainException (400)
        doctor.Activate();
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
