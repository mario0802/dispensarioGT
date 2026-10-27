using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Patients.Commands.CreatePatient;

/// <summary>
/// Registra un paciente nuevo; el documento (tipo + número) debe ser único.
/// </summary>
public sealed class CreatePatientHandler(IApplicationDbContext db, TimeProvider timeProvider)
    : ICommandHandler<CreatePatientCommand, CreatePatientResult>
{
    public async Task<CreatePatientResult> Handle(CreatePatientCommand command, CancellationToken cancellationToken)
    {
        var data = PatientData.From(
            command.DocumentType,
            command.DocumentNumber,
            command.FirstName,
            command.LastName,
            command.Email,
            command.Phone,
            command.Address,
            command.BloodType);
        var patient = Patient.Create(
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

        // Un owned type no se compara entero en LINQ: se comparan sus columnas
        if (await db.Patients.AnyAsync(
                p => p.Document.Type == patient.Document.Type && p.Document.Number == patient.Document.Number,
                cancellationToken))
            throw new ConflictException($"Patient with document {patient.Document} already exists.");

        db.Patients.Add(patient);
        await db.SaveChangesAsync(cancellationToken);

        return new CreatePatientResult(patient.Id.Value);
    }
}
