namespace Dispensario.Application.Abstractions;

/// <summary>
/// Conversión entre la hora local del dispensario (<c>Clinic:TimeZone</c>) y UTC.
/// </summary>
public interface IClinicTimeZone
{
    /// <summary>Convierte una hora local del dispensario (sin offset) a UTC (<c>Kind = Utc</c>).</summary>
    DateTime ToUtc(DateTime local);

    /// <summary>Convierte una hora UTC a la hora local del dispensario (<c>Kind = Unspecified</c>).</summary>
    DateTime ToLocal(DateTime utc);

    /// <summary>Fecha local de hoy en el dispensario según el <see cref="TimeProvider"/>.</summary>
    DateOnly Today { get; }
}
