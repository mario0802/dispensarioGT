using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Doctors.Commands.CreateDoctor;

/// <summary>
/// Registra un doctor nuevo; la matrícula debe ser única.
/// </summary>
public sealed class CreateDoctorHandler(IApplicationDbContext db) : ICommandHandler<CreateDoctorCommand, CreateDoctorResult>
{
    public async Task<CreateDoctorResult> Handle(CreateDoctorCommand command, CancellationToken cancellationToken)
    {
        var doctor = Doctor.Create(
            FullName.Create(command.FirstName, command.LastName),
            command.LicenseNumber,
            command.Specialty,
            Email.Create(command.Email),
            command.Phone is null ? null : PhoneNumber.Create(command.Phone),
            command.AppointmentDurationMinutes ?? Doctor.DefaultAppointmentDurationMinutes);

        // El aggregate ya normalizó la matrícula, así que la unicidad no distingue mayúsculas
        if (await db.Doctors.AnyAsync(d => d.LicenseNumber == doctor.LicenseNumber, cancellationToken))
            throw new ConflictException($"Doctor with license number {doctor.LicenseNumber} already exists.");

        db.Doctors.Add(doctor);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateDoctorResult(doctor.Id.Value);
    }
}
