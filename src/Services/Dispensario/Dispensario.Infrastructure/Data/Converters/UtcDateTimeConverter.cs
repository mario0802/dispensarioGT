using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dispensario.Infrastructure.Data.Converters;

/// <summary>
/// Marca como UTC los DateTime leídos de SQLite, que los devuelve con Kind = Unspecified.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

/// <summary>
/// Variante de <see cref="UtcDateTimeConverter"/> para DateTime?.
/// </summary>
public sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    value => value,
    value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);
