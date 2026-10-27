namespace Dispensario.Application.Common;

/// <summary>
/// Configuración del dispensario (sección <c>Clinic</c>).
/// </summary>
public sealed class ClinicOptions
{
    public const string SectionName = "Clinic";

    // ID IANA de la zona horaria: los horarios y las citas se expresan en esta hora local
    public string TimeZone { get; init; } = "America/Guayaquil";
}
