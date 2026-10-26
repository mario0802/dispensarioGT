using Dispensario.Application.Abstractions;
using Dispensario.Infrastructure.Authentication;
using Dispensario.Infrastructure.Data;
using Dispensario.Infrastructure.Data.Interceptors;
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

        services.AddScoped<ICurrentUser, AnonymousCurrentUser>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
