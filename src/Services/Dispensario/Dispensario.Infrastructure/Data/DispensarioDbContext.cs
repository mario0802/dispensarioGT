using Dispensario.Application.Abstractions;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Dispensario.Domain.Users;
using Dispensario.Infrastructure.Data.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Infrastructure.Data;

/// <summary>
/// Único DbContext del dispensario; cada módulo aporta sus DbSet y sus configuraciones.
/// </summary>
public sealed class DispensarioDbContext(DbContextOptions<DispensarioDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(DispensarioDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Ensamblado de Dispensario.Domain, localizado con un tipo ancla de ese proyecto
        configurationBuilder.ApplyStronglyTypedIdConventions(typeof(Email).Assembly);
        configurationBuilder.ApplyUtcDateTimeConventions();
    }
}
