using Dispensario.Domain.Common;

namespace Dispensario.Infrastructure.Data.Seed;

/// <summary>
/// Datos del usuario Admin que se siembra al arrancar (sección <c>Seed:Admin</c>).
/// </summary>
// Sin ValidateOnStart: una contraseña vacía es válida y solo hace que se omita el seed
public sealed class AdminSeedOptions
{
    public const string SectionName = "Seed:Admin";

    public string Email { get; init; } = string.Empty;

    // Va en user-secrets o variables de entorno, nunca en appsettings versionado
    public string Password { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public DocumentType DocumentType { get; init; }

    public string DocumentNumber { get; init; } = string.Empty;
}
