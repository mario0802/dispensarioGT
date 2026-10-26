using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Users.Commands.Login;

/// <summary>
/// Verifica email y contraseña y emite el JWT de acceso.
/// </summary>
public sealed class LoginHandler(IApplicationDbContext db, IPasswordHasher passwordHasher, IJwtTokenGenerator tokenGenerator)
    : ICommandHandler<LoginCommand, LoginResult>
{
    public const string TokenType = "Bearer";

    public async Task<LoginResult> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        // Email.Create normaliza a minúsculas, así que el login no distingue mayúsculas
        var email = Email.Create(command.Email);

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        // El mismo mensaje en los tres casos para no revelar qué emails existen
        if (user is null || !user.IsActive || !passwordHasher.Verify(user.PasswordHash, command.Password))
            throw new UnauthorizedException("Invalid credentials.");

        var (token, expiresAtUtc) = tokenGenerator.Generate(user);
        return new LoginResult(token, TokenType, expiresAtUtc);
    }
}
