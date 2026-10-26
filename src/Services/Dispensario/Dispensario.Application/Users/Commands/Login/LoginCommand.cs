using BuildingBlocks.CQRS;
using Dispensario.Domain.Common;
using FluentValidation;

namespace Dispensario.Application.Users.Commands.Login;

// Es un command, aunque no escribe, para que pase por ValidationBehavior
public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResult>;

public sealed record LoginResult(string AccessToken, string TokenType, DateTime ExpiresAtUtc);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(Email.MaxLength).EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
