using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Patients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Patients.Commands.UpdatePatient;

/// <summary>
/// Recepción edita los datos de un paciente; el documento sigue siendo único.
/// </summary>
public sealed class UpdatePatientHandler(IApplicationDbContext db, TimeProvider timeProvider) : ICommandHandler<UpdatePatientCommand>
{
    public async Task<Unit> Handle(UpdatePatientCommand command, CancellationToken cancellationToken)
    {
        var patientId = new PatientId(command.PatientId);

        var patient = await db.Patients.SingleOrDefaultAsync(p => p.Id == patientId, cancellationToken)
            ?? throw new NotFoundException(nameof(Patient), command.PatientId);

        var data = PatientData.From(
            command.DocumentType,
            command.DocumentNumber,
            command.FirstName,
            command.LastName,
            command.Email,
            command.Phone,
            command.Address,
            command.BloodType);
        patient.Update(
            data.Document,
            data.FullName,
            command.BirthDate,
            command.Gender,
            data.Email,
            data.Phone,
            data.Address,
            data.BloodType,
            command.Allergies,
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

        // La consulta va a la BD, no al change tracker: el propio paciente queda fuera por su ID
        if (await db.Patients.AnyAsync(
                p => p.Id != patientId &&
                     p.Document.Type == patient.Document.Type &&
                     p.Document.Number == patient.Document.Number,
                cancellationToken))
            throw new ConflictException($"Patient with document {patient.Document} already exists.");

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
