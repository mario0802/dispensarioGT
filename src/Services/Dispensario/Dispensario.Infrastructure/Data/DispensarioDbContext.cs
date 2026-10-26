using Dispensario.Application.Abstractions;
using Dispensario.Domain.Common;
using Dispensario.Infrastructure.Data.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Infrastructure.Data;

/// <summary>
/// Único DbContext del dispensario; cada módulo aporta sus DbSet y sus configuraciones.
/// </summary>
public sealed class DispensarioDbContext(DbContextOptions<DispensarioDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(DispensarioDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Ensamblado de Dispensario.Domain, localizado con un tipo ancla de ese proyecto
        configurationBuilder.ApplyStronglyTypedIdConventions(typeof(Email).Assembly);
        configurationBuilder.ApplyUtcDateTimeConventions();
    }
}
