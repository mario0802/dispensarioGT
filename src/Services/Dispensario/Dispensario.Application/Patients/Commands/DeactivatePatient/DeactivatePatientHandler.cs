using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Patients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Patients.Commands.DeactivatePatient;

/// <summary>
/// Desactiva un paciente; sus datos se conservan.
/// </summary>
public sealed class DeactivatePatientHandler(IApplicationDbContext db) : ICommandHandler<DeactivatePatientCommand>
{
    public async Task<Unit> Handle(DeactivatePatientCommand command, CancellationToken cancellationToken)
    {
        var patientId = new PatientId(command.PatientId);

        var patient = await db.Patients.SingleOrDefaultAsync(p => p.Id == patientId, cancellationToken)
            ?? throw new NotFoundException(nameof(Patient), command.PatientId);

        // Sobre un paciente ya inactivo, el dominio lanza DomainException (400)
        patient.Deactivate();
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
