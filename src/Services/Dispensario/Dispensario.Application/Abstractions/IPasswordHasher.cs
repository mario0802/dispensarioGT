namespace Dispensario.Application.Abstractions;

/// <summary>
/// Calcula y verifica hashes de contraseñas.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string hash, string password);
}
