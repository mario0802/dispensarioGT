namespace Dispensario.Api.Authorization;

/// <summary>
/// Nombres de las políticas de autorización por rol que usan los endpoints.
/// </summary>
public static class Policies
{
    // Admin
    public const string AdminOnly = nameof(AdminOnly);

    // Admin y Receptionist
    public const string Reception = nameof(Reception);

    // Doctor
    public const string Clinical = nameof(Clinical);
}
