using BuildingBlocks.Behaviors;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Common;
using FluentValidation;
using Mapster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dispensario.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra MediatR (handlers y behaviors), los validadores de FluentValidation, la configuración de Mapster
    /// y la zona horaria del dispensario.
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

        // Con una zona inexistente la aplicación no arranca: es mejor que fallar al agendar la primera cita
        services.AddOptions<ClinicOptions>()
            .Bind(configuration.GetSection(ClinicOptions.SectionName))
            .Validate(
                options => options.TimeZone is not null && TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZone, out _),
                "Clinic:TimeZone is not a valid time zone.")
            .ValidateOnStart();
        services.AddSingleton<IClinicTimeZone, ClinicTimeZone>();

        return services;
    }
}
