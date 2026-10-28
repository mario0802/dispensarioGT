using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
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
    public const string JwtIssuer = "Dispensario.Tests";
    public const string JwtAudience = "Dispensario.Tests.Clients";
    public const string JwtKey = "integration-tests-signing-key-with-32+-chars";
    public const string AdminEmail = "admin@dispensario.local";
    public const string AdminPassword = "Admin-Integration-Tests-1";
    public const string AllowedOrigin = "http://frontend.tests";

    private readonly SqliteConnection _connection = new(InMemoryConnectionString);

    public DispensarioApiFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Evitan depender de appsettings, de user-secrets y de dispensario.db
        builder.UseSetting("ConnectionStrings:Database", InMemoryConnectionString);
        builder.UseSetting("MediatR:LicenseKey", string.Empty);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("Seed:Admin:Email", AdminEmail);
        builder.UseSetting("Seed:Admin:Password", AdminPassword);
        // Sin datos de ejemplo: los tests crean sus propios datos
        builder.UseSetting("Seed:SampleData:Enabled", "false");
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);

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

    /// <summary>
    /// Crea un usuario activo del rol pedido, hace login por HTTP y devuelve un cliente con su token.
    /// </summary>
    public async Task<HttpClient> AuthenticateAsAsync(Role role, CancellationToken cancellationToken = default)
    {
        // Email y documento únicos por llamada: varios tests comparten la misma base de datos
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var email = $"{role.ToString().ToLowerInvariant()}.{suffix}@dispensario.local";
        const string password = "User-Integration-Tests-1";

        using (var scope = Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var dbContext = scope.ServiceProvider.GetRequiredService<DispensarioDbContext>();

            dbContext.Users.Add(User.Create(
                Email.Create(email),
                hasher.Hash(password),
                FullName.Create("Test", role.ToString()),
                Document.Create(DocumentType.Passport, suffix),
                phone: null,
                role));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponseBody>(cancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _connection.Dispose();
    }
}

// Solo el campo que necesita AuthenticateAsAsync de la respuesta del login
internal sealed record LoginResponseBody(string AccessToken);
