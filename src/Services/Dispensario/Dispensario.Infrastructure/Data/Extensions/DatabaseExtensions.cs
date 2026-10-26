using Dispensario.Infrastructure.Data.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dispensario.Infrastructure.Data.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Crea la base de datos si no existe y siembra el usuario Admin.
    /// </summary>
    // Sin migraciones hasta la fila 17: EnsureCreated no actualiza una base ya creada si cambia el modelo.
    // La fila 17 lo sustituye por MigrateAsync
    public static async Task InitialiseDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        await using var scope = host.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<DispensarioDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }
}
