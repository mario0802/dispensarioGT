using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Appointments.Common;

/// <summary>
/// Reglas de agenda que comparten el alta y la reprogramación de citas: horario, alineación y cruces.
/// </summary>
public static class AppointmentSlotRules
{
    // Horas locales del dispensario: el horario del doctor está en hora local
    public static void EnsureFitsSchedule(Doctor doctor, DateTime localStart, DateTime localEnd)
    {
        if (!doctor.IsAvailableAt(localStart, localEnd))
            throw new BadRequestException("The doctor is not available at that time.");

        if (!IsAlignedToSlots(doctor, localStart, localEnd))
            throw new BadRequestException("Start time must align with the doctor's appointment slots.");
    }

    // Solo cuentan las citas Scheduled o Confirmed; excludedAppointmentId evita que una reprogramación choque consigo misma
    public static async Task EnsureNoOverlapsAsync(
        IApplicationDbContext db,
        DoctorId doctorId,
        PatientId patientId,
        DateTime startUtc,
        DateTime endUtc,
        AppointmentId? excludedAppointmentId,
        CancellationToken cancellationToken)
    {
        var overlapping = db.Appointments.Where(a =>
            (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed)
            && a.StartUtc < endUtc && startUtc < a.EndUtc);

        if (excludedAppointmentId is { } excludedId)
            overlapping = overlapping.Where(a => a.Id != excludedId);

        if (await overlapping.AnyAsync(a => a.DoctorId == doctorId, cancellationToken))
            throw new ConflictException("The doctor already has an appointment at that time.");

        if (await overlapping.AnyAsync(a => a.PatientId == patientId, cancellationToken))
            throw new ConflictException("The patient already has an appointment at that time.");
    }

    // Slots del horario del doctor en una fecha local, ordenados por inicio (Kind = Unspecified).
    // Solo mira el horario: ni IsActive, ni el pasado, ni las citas. El sobrante final de un bloque se descarta
    public static IReadOnlyList<(DateTime LocalStart, DateTime LocalEnd)> GetScheduleSlots(Doctor doctor, DateOnly date)
    {
        var duration = TimeSpan.FromMinutes(doctor.AppointmentDurationMinutes);
        var slots = new List<(DateTime LocalStart, DateTime LocalEnd)>();

        foreach (var block in doctor.Schedule.Where(b => b.DayOfWeek == date.DayOfWeek).OrderBy(b => b.StartTime))
        {
            var start = date.ToDateTime(block.StartTime);
            var blockEnd = date.ToDateTime(block.EndTime);

            for (var end = start + duration; end <= blockEnd; start = end, end += duration)
                slots.Add((start, end));
        }

        return slots;
    }

    // Los slots se cuentan desde el inicio del bloque que contiene la cita, en pasos de la duración
    private static bool IsAlignedToSlots(Doctor doctor, DateTime localStart, DateTime localEnd)
    {
        var duration = localEnd - localStart;
        var start = TimeOnly.FromDateTime(localStart);
        var end = TimeOnly.FromDateTime(localEnd);
        var block = doctor.Schedule.First(b => b.DayOfWeek == localStart.DayOfWeek && b.Contains(start, end));

        return (start - block.StartTime).Ticks % duration.Ticks == 0;
    }
}
