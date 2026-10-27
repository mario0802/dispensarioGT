using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Appointments.Common;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Appointments.Commands.RescheduleAppointment;

/// <summary>
/// Mueve una cita a otra hora con el mismo doctor y paciente, con las mismas reglas que el alta.
/// </summary>
public sealed class RescheduleAppointmentHandler(
    IApplicationDbContext db,
    IClinicTimeZone clinicTimeZone,
    TimeProvider timeProvider)
    : ICommandHandler<RescheduleAppointmentCommand>
{
    public async Task<Unit> Handle(RescheduleAppointmentCommand command, CancellationToken cancellationToken)
    {
        var appointmentId = new AppointmentId(command.AppointmentId);
        var appointment = await db.Appointments.SingleOrDefaultAsync(a => a.Id == appointmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Appointment), command.AppointmentId);

        var patient = await db.Patients.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == appointment.PatientId, cancellationToken)
            ?? throw new NotFoundException(nameof(Patient), appointment.PatientId.Value);
        if (!patient.IsActive)
            throw new BadRequestException("Patient is inactive.");

        var doctor = await db.Doctors.AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == appointment.DoctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Doctor), appointment.DoctorId.Value);
        if (!doctor.IsActive)
            throw new BadRequestException("Doctor is inactive.");

        // Duración vigente del doctor, no la de la cita original
        var duration = TimeSpan.FromMinutes(doctor.AppointmentDurationMinutes);
        var localStart = command.StartLocal;
        var localEnd = localStart + duration;
        var startUtc = clinicTimeZone.ToUtc(localStart);
        var endUtc = startUtc + duration;

        // Estado, momento, misma hora o pasado → DomainException (400). Si algo falla después, no se guarda nada
        appointment.Reschedule(startUtc, endUtc, timeProvider.GetUtcNow().UtcDateTime);

        AppointmentSlotRules.EnsureFitsSchedule(doctor, localStart, localEnd);

        await AppointmentSlotRules.EnsureNoOverlapsAsync(
            db, doctor.Id, patient.Id, startUtc, endUtc, appointment.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
