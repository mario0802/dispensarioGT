using System.ComponentModel.DataAnnotations;

namespace Dispensario.Infrastructure.Authentication;

/// <summary>
/// Configuración del JWT (sección <c>Jwt</c>); la comparten el emisor y la validación de la Api.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public required string Issuer { get; init; }

    [Required]
    public required string Audience { get; init; }

    // Clave HS256: va en user-secrets o variables de entorno, nunca en appsettings versionado
    [Required, MinLength(32)]
    public required string Key { get; init; }

    [Range(1, 1440)]
    public int ExpirationMinutes { get; init; } = 60;
}
