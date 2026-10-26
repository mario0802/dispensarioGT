using Dispensario.Application.Abstractions;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Dispensario.Infrastructure.Authentication;

/// <summary>
/// Usuario actual leído de los claims del JWT de la petición HTTP.
/// </summary>
// Sin HttpContext (seed, procesos en segundo plano) se comporta como un usuario anónimo
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    // Claims propios del token; sub, email y name son los registrados de JWT
    public const string RoleClaimType = "role";
    public const string DoctorIdClaimType = "doctor_id";

    public UserId? UserId => TryGetGuid(JwtRegisteredClaimNames.Sub) is { } id ? new UserId(id) : null;

    public Role? Role => Enum.TryParse<Role>(FindClaim(RoleClaimType), out var role) && Enum.IsDefined(role) ? role : null;

    public DoctorId? DoctorId => TryGetGuid(DoctorIdClaimType) is { } id ? new DoctorId(id) : null;

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    private string? FindClaim(string claimType) =>
        IsAuthenticated ? httpContextAccessor.HttpContext!.User.FindFirst(claimType)?.Value : null;

    private Guid? TryGetGuid(string claimType) =>
        Guid.TryParse(FindClaim(claimType), out var value) ? value : null;
}
