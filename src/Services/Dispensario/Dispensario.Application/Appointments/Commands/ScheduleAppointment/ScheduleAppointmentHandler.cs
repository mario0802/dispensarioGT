using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Appointments.Common;
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

        AppointmentSlotRules.EnsureFitsSchedule(doctor, localStart, localEnd);

        await AppointmentSlotRules.EnsureNoOverlapsAsync(
            db, doctor.Id, patient.Id, startUtc, endUtc, excludedAppointmentId: null, cancellationToken);

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(cancellationToken);

        return new ScheduleAppointmentResult(appointment.Id.Value);
    }
}
