using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Appointments.Commands.ScheduleAppointment;

/// <summary>
/// Agenda una cita validando el horario del doctor, la alineación a sus slots y que no haya cruces.
/// </summary>
public sealed class ScheduleAppointmentHandler(
    IApplicationDbContext db,
    IClinicTimeZone clinicTimeZone,
    TimeProvider timeProvider)
    : ICommandHandler<ScheduleAppointmentCommand, ScheduleAppointmentResult>
{
    public async Task<ScheduleAppointmentResult> Handle(
        ScheduleAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        var patientId = new PatientId(command.PatientId);
        var patient = await db.Patients.AsNoTracking().SingleOrDefaultAsync(p => p.Id == patientId, cancellationToken)
            ?? throw new NotFoundException(nameof(Patient), command.PatientId);
        if (!patient.IsActive)
            throw new BadRequestException("Patient is inactive.");

        var doctorId = new DoctorId(command.DoctorId);
        var doctor = await db.Doctors.AsNoTracking().SingleOrDefaultAsync(d => d.Id == doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Doctor), command.DoctorId);
        if (!doctor.IsActive)
            throw new BadRequestException("Doctor is inactive.");

        // El horario está en hora local; la cita se guarda en UTC
        var duration = TimeSpan.FromMinutes(doctor.AppointmentDurationMinutes);
        var localStart = command.StartLocal;
        var localEnd = localStart + duration;
        var startUtc = clinicTimeZone.ToUtc(localStart);
        var endUtc = startUtc + duration;

        var appointment = Appointment.Create(
            patient.Id,
            doctor.Id,
            startUtc,
            endUtc,
            command.Reason,
            timeProvider.GetUtcNow().UtcDateTime);

        if (!doctor.IsAvailableAt(localStart, localEnd))
            throw new BadRequestException("The doctor is not available at that time.");

        if (!IsAlignedToSlots(doctor, localStart, localEnd, duration))
            throw new BadRequestException("Start time must align with the doctor's appointment slots.");

        // Se excluyen las citas canceladas, no asistidas o completadas del cálculo de cruces
        if (await db.Appointments.AnyAsync(
                a => a.DoctorId == doctor.Id
                    && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed)
                    && a.StartUtc < endUtc && startUtc < a.EndUtc,
                cancellationToken))
            throw new ConflictException("The doctor already has an appointment at that time.");

        if (await db.Appointments.AnyAsync(
                a => a.PatientId == patient.Id
                    && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed)
                    && a.StartUtc < endUtc && startUtc < a.EndUtc,
                cancellationToken))
            throw new ConflictException("The patient already has an appointment at that time.");

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(cancellationToken);

        return new ScheduleAppointmentResult(appointment.Id.Value);
    }

    // Los slots se cuentan desde el inicio del bloque que contiene la cita, en pasos de la duración
    private static bool IsAlignedToSlots(Doctor doctor, DateTime localStart, DateTime localEnd, TimeSpan duration)
    {
        var start = TimeOnly.FromDateTime(localStart);
        var end = TimeOnly.FromDateTime(localEnd);
        var block = doctor.Schedule.First(b => b.DayOfWeek == localStart.DayOfWeek && b.Contains(start, end));

        return (start - block.StartTime).Ticks % duration.Ticks == 0;
    }
}
