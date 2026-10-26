using Dispensario.Infrastructure.Data.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Persistence.Fakes;

// Reproduce las convenciones de DispensarioDbContext sobre el aggregate de prueba
public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestAggregate> Aggregates => Set<TestAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var aggregate = modelBuilder.Entity<TestAggregate>();
        aggregate.ToTable("TestAggregates");
        aggregate.HasKey(x => x.Id);
        // Las propiedades de solo lectura de los VOs no se mapean por convención: se declaran una a una
        aggregate.ComplexProperty(x => x.Name, name =>
        {
            name.Property(n => n.FirstName);
            name.Property(n => n.LastName);
        });
        aggregate.ComplexProperty(x => x.Address, address =>
        {
            address.Property(a => a.Street);
            address.Property(a => a.City);
            address.Property(a => a.State);
            address.Property(a => a.Reference);
        });
        aggregate.OwnsMany(x => x.Children, children =>
        {
            children.ToTable("TestChildren");
            children.WithOwner().HasForeignKey("TestAggregateId");
            children.Property<int>("Id");
            children.HasKey("Id");
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyStronglyTypedIdConventions(typeof(TestDbContext).Assembly);
        configurationBuilder.ApplyUtcDateTimeConventions();
    }
}
