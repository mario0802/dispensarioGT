using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Dispensario.Application.Common;

/// <summary>
/// Implementación de <see cref="IClinicTimeZone"/> con <see cref="TimeZoneInfo"/> y la zona de <see cref="ClinicOptions"/>.
/// </summary>
public sealed class ClinicTimeZone(IOptions<ClinicOptions> options, TimeProvider timeProvider) : IClinicTimeZone
{
    // La zona ya se validó al arrancar (ValidateOnStart): aquí siempre existe
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateOnly Today => DateOnly.FromDateTime(ToLocal(timeProvider.GetUtcNow().UtcDateTime));

    public DateTime ToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        // Una hora que se salta el cambio al horario de verano no existe en la zona
        if (_timeZone.IsInvalidTime(unspecified))
            throw new BadRequestException("Start time does not exist in the clinic time zone.");

        // Una hora ambigua (vuelta al horario estándar) se toma como hora estándar
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, _timeZone);
    }

    public DateTime ToLocal(DateTime utc) =>
        DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone),
            DateTimeKind.Unspecified);
}
