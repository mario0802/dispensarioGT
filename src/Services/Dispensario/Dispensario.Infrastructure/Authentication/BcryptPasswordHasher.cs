using Dispensario.Application.Abstractions;

namespace Dispensario.Infrastructure.Authentication;

/// <summary>
/// Hash de contraseñas con bcrypt (BCrypt.Net-Next); la sal va incluida en el propio hash.
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    // Coste 2^12: unos 250 ms por hash
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string hash, string password) => BCrypt.Net.BCrypt.Verify(password, hash);
}
