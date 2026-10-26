using Dispensario.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dispensario.Api.IntegrationTests.Common;

// Host real en Development sobre SQLite en memoria; la conexión sigue abierta mientras viva la factory
public sealed class DispensarioApiFactory : WebApplicationFactory<Program>
{
    public const string InMemoryConnectionString = "Data Source=:memory:";

    private readonly SqliteConnection _connection = new(InMemoryConnectionString);

    public DispensarioApiFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Evitan depender de appsettings y de dispensario.db
        builder.UseSetting("ConnectionStrings:Database", InMemoryConnectionString);
        builder.UseSetting("MediatR:LicenseKey", string.Empty);

        builder.ConfigureTestServices(services =>
        {
            // En EF Core 10 AddDbContext acumula configuraciones: se quitan las originales antes de registrar la de test
            services.RemoveAll<DbContextOptions<DispensarioDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<DispensarioDbContext>>();

            services.AddDbContext<DispensarioDbContext>((sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                options.UseSqlite(_connection);
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _connection.Dispose();
    }
}
