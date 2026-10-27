using Dispensario.Application.Abstractions;
using Dispensario.Domain.Consultations;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;

namespace Dispensario.Application.Consultations.Dtos;

/// <summary>
/// Proyección de consultas a <see cref="ConsultationDto"/> que comparten las queries del módulo.
/// </summary>
public static class ConsultationProjections
{
    // Los nombres salen de subconsultas sobre Patients y Doctors (lectura entre módulos, sin navegaciones).
    // Las horas locales no se traducen a SQL: salen vacías y las rellena WithLocalTimes en memoria
    public static IQueryable<ConsultationDto> ToDto(
        this IQueryable<Consultation> consultations,
        IQueryable<Patient> patients,
        IQueryable<Doctor> doctors) =>
        consultations.Select(c => new ConsultationDto(
            c.Id.Value,
            c.PatientId.Value,
            patients
                .Where(p => p.Id == c.PatientId)
                .Select(p => p.FullName.FirstName + " " + p.FullName.LastName)
                .FirstOrDefault() ?? string.Empty,
            c.DoctorId.Value,
            doctors
                .Where(d => d.Id == c.DoctorId)
                .Select(d => d.FullName.FirstName + " " + d.FullName.LastName)
                .FirstOrDefault() ?? string.Empty,
            c.AppointmentId == null ? (Guid?)null : c.AppointmentId.Value.Value,
            c.Reason,
            c.Diagnosis,
            c.Notes,
            c.Status,
            c.StartedAtUtc,
            c.ClosedAtUtc,
            default,
            null,
            c.CreatedAtUtc));

    public static ConsultationDto WithLocalTimes(this ConsultationDto dto, IClinicTimeZone clinicTimeZone) =>
        dto with
        {
            StartedAtLocal = clinicTimeZone.ToLocal(dto.StartedAtUtc),
            ClosedAtLocal = dto.ClosedAtUtc is { } closedAtUtc ? clinicTimeZone.ToLocal(closedAtUtc) : null
        };
}
