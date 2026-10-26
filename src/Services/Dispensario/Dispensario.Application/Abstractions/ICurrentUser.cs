namespace Dispensario.Application.Abstractions;

/// <summary>
/// Usuario que hace la petición actual.
/// </summary>
// Versión mínima: la fila 07 cambia UserId a UserId? y añade Role y DoctorId
public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}
