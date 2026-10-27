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

    // Admin y Doctor; si el recurso es de un doctor concreto, el handler comprueba que sea el suyo
    public const string AdminOrDoctor = nameof(AdminOrDoctor);
}
