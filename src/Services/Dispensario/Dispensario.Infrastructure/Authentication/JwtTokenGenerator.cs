using System.Text;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dispensario.Infrastructure.Authentication;

/// <summary>
/// Emite JWT firmados con HS256 con los claims del usuario.
/// </summary>
public sealed class JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider timeProvider) : IJwtTokenGenerator
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTime ExpiresAtUtc) Generate(User user)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAtUtc = now.AddMinutes(jwt.ExpirationMinutes);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.Value.ToString(),
            [JwtRegisteredClaimNames.Email] = user.Email.Value,
            [JwtRegisteredClaimNames.Name] = user.FullName.ToString(),
            [CurrentUser.RoleClaimType] = user.Role.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
        };

        if (user.DoctorId is { } doctorId)
            claims[CurrentUser.DoctorIdClaimType] = doctorId.Value.ToString();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAtUtc,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                SecurityAlgorithms.HmacSha256),
        };

        return (_handler.CreateToken(descriptor), expiresAtUtc);
    }
}
