using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Patients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Patients.Commands.ActivatePatient;

/// <summary>
/// Vuelve a activar un paciente desactivado.
/// </summary>
public sealed class ActivatePatientHandler(IApplicationDbContext db) : ICommandHandler<ActivatePatientCommand>
{
    public async Task<Unit> Handle(ActivatePatientCommand command, CancellationToken cancellationToken)
    {
        var patientId = new PatientId(command.PatientId);

        var patient = await db.Patients.SingleOrDefaultAsync(p => p.Id == patientId, cancellationToken)
            ?? throw new NotFoundException(nameof(Patient), command.PatientId);

        // Sobre un paciente ya activo, el dominio lanza DomainException (400)
        patient.Activate();
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
