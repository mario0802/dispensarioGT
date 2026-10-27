using Dispensario.Application.Abstractions;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;

namespace Dispensario.Application.Appointments.Dtos;

/// <summary>
/// Proyección de citas a <see cref="AppointmentDto"/> que comparten las queries del módulo.
/// </summary>
public static class AppointmentProjections
{
    // Los nombres salen de subconsultas sobre Patients y Doctors (lectura entre módulos, sin navegaciones).
    // Las horas locales no se traducen a SQL: salen vacías y las rellena WithLocalTimes en memoria
    public static IQueryable<AppointmentDto> ToDto(
        this IQueryable<Appointment> appointments,
        IQueryable<Patient> patients,
        IQueryable<Doctor> doctors) =>
        appointments.Select(a => new AppointmentDto(
            a.Id.Value,
            a.PatientId.Value,
            patients
                .Where(p => p.Id == a.PatientId)
                .Select(p => p.FullName.FirstName + " " + p.FullName.LastName)
                .FirstOrDefault() ?? string.Empty,
            a.DoctorId.Value,
            doctors
                .Where(d => d.Id == a.DoctorId)
                .Select(d => d.FullName.FirstName + " " + d.FullName.LastName)
                .FirstOrDefault() ?? string.Empty,
            a.StartUtc,
            a.EndUtc,
            default,
            default,
            a.Reason,
            a.Status,
            a.CancellationReason,
            a.CreatedAtUtc));

    public static AppointmentDto WithLocalTimes(this AppointmentDto dto, IClinicTimeZone clinicTimeZone) =>
        dto with
        {
            StartLocal = clinicTimeZone.ToLocal(dto.StartUtc),
            EndLocal = clinicTimeZone.ToLocal(dto.EndUtc)
        };
}
