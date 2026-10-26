using BuildingBlocks.Behaviors;
using FluentValidation;
using Mapster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dispensario.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra MediatR (handlers y behaviors), los validadores de FluentValidation y la configuración de Mapster.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            // Solo se asigna si hay clave: una cadena vacía no es una licencia válida
            var licenseKey = configuration["MediatR:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(licenseKey))
                cfg.LicenseKey = licenseKey;

            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly);
        TypeAdapterConfig.GlobalSettings.Scan(assembly);

        return services;
    }
}
