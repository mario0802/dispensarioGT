using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Consultations;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Consultations.Commands.StartConsultation;

/// <summary>
/// El doctor actual inicia una consulta, con o sin cita; una cita de hoy solo admite una consulta.
/// </summary>
public sealed class StartConsultationHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IClinicTimeZone clinicTimeZone,
    TimeProvider timeProvider)
    : ICommandHandler<StartConsultationCommand, StartConsultationResult>
{
    public async Task<StartConsultationResult> Handle(StartConsultationCommand command, CancellationToken cancellationToken)
    {
        // La política Clinical garantiza el rol Doctor; aquí solo falta el vínculo
        var doctorId = currentUser.DoctorId
            ?? throw new ForbiddenException("Your user is not linked to a doctor.");

        var doctor = await db.Doctors.AsNoTracking().SingleOrDefaultAsync(d => d.Id == doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Doctor), doctorId.Value);
        if (!doctor.IsActive)
            throw new BadRequestException("Doctor is inactive.");

        var patientId = new PatientId(command.PatientId);
        var patient = await db.Patients.AsNoTracking().SingleOrDefaultAsync(p => p.Id == patientId, cancellationToken)
            ?? throw new NotFoundException(nameof(Patient), command.PatientId);
        if (!patient.IsActive)
            throw new BadRequestException("Patient is inactive.");

        AppointmentId? appointmentId = null;
        if (command.AppointmentId is { } appointmentGuid)
        {
            appointmentId = new AppointmentId(appointmentGuid);
            await EnsureAppointmentCanStartAsync(appointmentId.Value, doctorId, patientId, cancellationToken);
        }

        var consultation = Consultation.Start(
            patientId,
            doctorId,
            appointmentId,
            command.Reason,
            timeProvider.GetUtcNow().UtcDateTime);

        db.Consultations.Add(consultation);
        await db.SaveChangesAsync(cancellationToken);

        return new StartConsultationResult(consultation.Id.Value);
    }

    // Reglas de la cita: del doctor actual y del paciente, pendiente, de hoy (fecha local) y sin consulta previa
    private async Task EnsureAppointmentCanStartAsync(
        AppointmentId appointmentId,
        DoctorId doctorId,
        PatientId patientId,
        CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == appointmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId.Value);

        if (appointment.DoctorId != doctorId)
            throw new ForbiddenException("You can only manage your own appointments.");

        if (appointment.PatientId != patientId)
            throw new BadRequestException("Appointment does not belong to the patient.");

        if (appointment.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
            throw new BadRequestException("Only scheduled or confirmed appointments can start a consultation.");

        if (DateOnly.FromDateTime(clinicTimeZone.ToLocal(appointment.StartUtc)) != clinicTimeZone.Today)
            throw new BadRequestException("Appointment is not scheduled for today.");

        // Una sola consulta por cita, abierta o cerrada (también lo garantiza el índice único)
        if (await db.Consultations.AnyAsync(c => c.AppointmentId == appointmentId, cancellationToken))
            throw new ConflictException("The appointment already has a consultation.");
    }
}
