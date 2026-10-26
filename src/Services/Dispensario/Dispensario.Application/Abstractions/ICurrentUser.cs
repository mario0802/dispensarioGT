using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;

namespace Dispensario.Application.Abstractions;

/// <summary>
/// Usuario que hace la petición actual; todo es <c>null</c> si la petición es anónima.
/// </summary>
public interface ICurrentUser
{
    UserId? UserId { get; }

    Role? Role { get; }

    DoctorId? DoctorId { get; }

    bool IsAuthenticated { get; }
}
