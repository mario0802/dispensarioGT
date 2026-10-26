using Dispensario.Application.Abstractions;
using Dispensario.Infrastructure.Authentication;
using Dispensario.Infrastructure.Data;
using Dispensario.Infrastructure.Data.Interceptors;
using Dispensario.Infrastructure.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dispensario.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra la persistencia (DbContext sobre SQLite e interceptores) y los servicios de infraestructura.
    /// </summary>
    // IPublisher, que usa DispatchDomainEventsInterceptor, lo registra MediatR en AddApplication (fila 05)
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Se lee aquí para que una configuración incompleta falle al arrancar y no en la primera petición
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' not found.");

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<DispensarioDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlite(connectionString);
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<DispensarioDbContext>());

        // Sin una clave válida la aplicación no arranca: es mejor que fallar en el primer login
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<AdminSeedOptions>().BindConfiguration(AdminSeedOptions.SectionName);
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
