using System.Linq.Expressions;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dispensario.Infrastructure.Data.Converters;

/// <summary>
/// Guarda un ID tipado como su Guid y lo reconstruye con su constructor (Guid).
/// </summary>
public sealed class StronglyTypedIdConverter<TId>() : ValueConverter<TId, Guid>(id => id.Value, CreateFromGuid())
    where TId : struct, IStronglyTypedId
{
    // Árbol de expresión en lugar de Activator para que EF pueda traducirlo y no use reflexión por fila
    private static Expression<Func<Guid, TId>> CreateFromGuid()
    {
        var constructor = typeof(TId).GetConstructor([typeof(Guid)])
            ?? throw new InvalidOperationException(
                $"Strongly typed ID '{typeof(TId).Name}' must have a public constructor that takes a Guid.");

        var value = Expression.Parameter(typeof(Guid), "value");
        return Expression.Lambda<Func<Guid, TId>>(Expression.New(constructor, value), value);
    }
}
