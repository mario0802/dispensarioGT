using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Appointments.Common;
using Dispensario.Application.Appointments.Dtos;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Appointments.Queries.GetAvailableSlots;

/// <summary>
/// Devuelve los slots libres de un doctor en una fecha local: los de su horario, futuros y sin cruce
/// con sus citas Scheduled o Confirmed. No mira las citas del paciente.
/// </summary>
public sealed class GetAvailableSlotsHandler(
    IApplicationDbContext db,
    IClinicTimeZone clinicTimeZone,
    TimeProvider timeProvider)
    : IQueryHandler<GetAvailableSlotsQuery, AvailableSlotsDto>
{
    public async Task<AvailableSlotsDto> Handle(GetAvailableSlotsQuery query, CancellationToken cancellationToken)
    {
        // Las queries no pasan por ValidationBehavior: la fecha se valida aquí, antes de leer la BD
        if (query.Date < clinicTimeZone.Today)
            throw new BadRequestException("Date cannot be in the past.");

        var doctorId = new DoctorId(query.DoctorId);
        var doctor = await db.Doctors.AsNoTracking().SingleOrDefaultAsync(d => d.Id == doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Doctor), query.DoctorId);

        if (!doctor.IsActive)
            throw new BadRequestException("Doctor is inactive.");

        var scheduleSlots = AppointmentSlotRules.GetScheduleSlots(doctor, query.Date);
        if (scheduleSlots.Count == 0)
            return new AvailableSlotsDto(query.DoctorId, query.Date, doctor.AppointmentDurationMinutes, []);

        // Igual que Appointment.Create: un slot que empieza justo ahora ya no se ofrece
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var futureSlots = scheduleSlots
            .Select(slot => (slot.LocalStart, slot.LocalEnd,
                StartUtc: clinicTimeZone.ToUtc(slot.LocalStart), EndUtc: clinicTimeZone.ToUtc(slot.LocalEnd)))
            .Where(slot => slot.StartUtc > nowUtc)
            .ToList();

        var dayStartUtc = clinicTimeZone.ToUtc(query.Date.ToDateTime(TimeOnly.MinValue));
        var dayEndUtc = clinicTimeZone.ToUtc(query.Date.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var busy = await db.Appointments
            .AsNoTracking()
            .Where(a => a.DoctorId == doctorId
                && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed)
                && a.StartUtc < dayEndUtc && dayStartUtc < a.EndUtc)
            .Select(a => new { a.StartUtc, a.EndUtc })
            .ToListAsync(cancellationToken);

        // En memoria: un día tiene pocas decenas de slots y citas. Una cita contigua no es cruce
        var slots = futureSlots
            .Where(slot => !busy.Any(a => a.StartUtc < slot.EndUtc && slot.StartUtc < a.EndUtc))
            .Select(slot => new AvailableSlotDto(slot.LocalStart, slot.LocalEnd))
            .ToList();

        return new AvailableSlotsDto(query.DoctorId, query.Date, doctor.AppointmentDurationMinutes, slots);
    }
}
