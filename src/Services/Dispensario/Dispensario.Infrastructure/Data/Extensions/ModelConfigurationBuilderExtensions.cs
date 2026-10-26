using System.Reflection;
using BuildingBlocks.Domain;
using Dispensario.Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Infrastructure.Data.Extensions;

/// <summary>
/// Convenciones del modelo compartidas por todos los DbContext del dispensario.
/// </summary>
public static class ModelConfigurationBuilderExtensions
{
    /// <summary>
    /// Convierte a Guid todos los IDs tipados (<see cref="IStronglyTypedId"/>) declarados en el ensamblado.
    /// </summary>
    public static ModelConfigurationBuilder ApplyStronglyTypedIdConventions(
        this ModelConfigurationBuilder configurationBuilder, Assembly assembly)
    {
        var idTypes = assembly.GetTypes()
            .Where(type => type is { IsValueType: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && typeof(IStronglyTypedId).IsAssignableFrom(type));

        foreach (var idType in idTypes)
        {
            // Se valida aquí para que el error aparezca al construir el modelo con un mensaje claro
            if (idType.GetConstructor([typeof(Guid)]) is null)
                throw new InvalidOperationException(
                    $"Strongly typed ID '{idType.Name}' must have a public constructor that takes a Guid.");

            configurationBuilder
                .Properties(idType)
                .HaveConversion(typeof(StronglyTypedIdConverter<>).MakeGenericType(idType));
        }

        return configurationBuilder;
    }

    /// <summary>
    /// Hace que todo DateTime y DateTime? leído de la base de datos tenga Kind = Utc.
    /// </summary>
    public static ModelConfigurationBuilder ApplyUtcDateTimeConventions(this ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        return configurationBuilder;
    }
}
