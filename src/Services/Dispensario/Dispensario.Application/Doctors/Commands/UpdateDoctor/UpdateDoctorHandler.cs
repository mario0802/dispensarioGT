using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Doctors.Commands.UpdateDoctor;

/// <summary>
/// El Admin edita los datos de un doctor; la matrícula sigue siendo única.
/// </summary>
public sealed class UpdateDoctorHandler(IApplicationDbContext db) : ICommandHandler<UpdateDoctorCommand>
{
    public async Task<Unit> Handle(UpdateDoctorCommand command, CancellationToken cancellationToken)
    {
        var doctorId = new DoctorId(command.DoctorId);

        var doctor = await db.Doctors.SingleOrDefaultAsync(d => d.Id == doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Doctor), command.DoctorId);

        doctor.Update(
            FullName.Create(command.FirstName, command.LastName),
            command.LicenseNumber,
            command.Specialty,
            Email.Create(command.Email),
            command.Phone is null ? null : PhoneNumber.Create(command.Phone),
            command.AppointmentDurationMinutes ?? doctor.AppointmentDurationMinutes);

        // Se compara con la matrícula ya normalizada por el aggregate; la consulta va a la BD, no al change tracker
        if (await db.Doctors.AnyAsync(d => d.Id != doctorId && d.LicenseNumber == doctor.LicenseNumber, cancellationToken))
            throw new ConflictException($"Doctor with license number {doctor.LicenseNumber} already exists.");

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
