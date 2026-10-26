using Dispensario.Application.Abstractions;

namespace Dispensario.Infrastructure.Authentication;

/// <summary>
/// Usuario anónimo: implementación provisional de <see cref="ICurrentUser"/> hasta que llegue la autenticación (fila 07).
/// </summary>
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public Guid? UserId => null;

    public bool IsAuthenticated => false;
}
