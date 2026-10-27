using Dispensario.Domain.Appointments;
using Dispensario.Domain.Consultations;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Abstractions;

/// <summary>
/// Unidad de trabajo de la aplicación sobre la base de datos del dispensario.
/// </summary>
public interface IApplicationDbContext
{
    // Cada spec de módulo añade aquí su DbSet<T>
    DbSet<User> Users { get; }
    DbSet<Doctor> Doctors { get; }
    DbSet<Patient> Patients { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<Consultation> Consultations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
