using Dispensario.Infrastructure.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dispensario.Infrastructure.Data.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Aplica las migraciones pendientes y siembra el usuario Admin y los datos de ejemplo.
    /// </summary>
    // Una base creada antes con EnsureCreated no tiene __EFMigrationsHistory: hay que borrarla una vez
    public static async Task InitialiseDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        await using var scope = host.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<DispensarioDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(cancellationToken);

        var sampleDataSeeder = scope.ServiceProvider.GetRequiredService<SampleDataSeeder>();
        await sampleDataSeeder.SeedAsync(cancellationToken);
    }
}
