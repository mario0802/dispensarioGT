using Dispensario.Domain.Users;

namespace Dispensario.Application.Abstractions;

/// <summary>
/// Emite el JWT de acceso de un usuario.
/// </summary>
public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) Generate(User user);
}
