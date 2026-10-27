using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;

namespace Dispensario.Application.Doctors.Dtos;

/// <summary>
/// Proyección de doctores a <see cref="DoctorDto"/> que comparten las queries del módulo.
/// </summary>
public static class DoctorProjections
{
    // Mapster no expresa la subconsulta sobre Users (lectura entre módulos), por eso la proyección es manual
    public static IQueryable<DoctorDto> ToDto(this IQueryable<Doctor> doctors, IQueryable<User> users) =>
        doctors.Select(d => new DoctorDto(
            d.Id.Value,
            d.FullName.FirstName,
            d.FullName.LastName,
            d.LicenseNumber,
            d.Specialty,
            d.Email.Value,
            d.Phone == null ? null : d.Phone.Value,
            d.AppointmentDurationMinutes,
            d.IsActive,
            users.Where(u => u.DoctorId == d.Id).Select(u => (Guid?)u.Id.Value).FirstOrDefault(),
            d.CreatedAtUtc));
}
