using BuildingBlocks.CQRS;
using Dispensario.Application.Users.Validation;
using FluentValidation;

namespace Dispensario.Application.Users.Commands.ResetUserPassword;

// El Admin fija una contraseña nueva a otro usuario, sin conocer la actual
public sealed record ResetUserPasswordCommand(Guid UserId, string NewPassword) : ICommand;

public sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.NewPassword).ValidPassword();
    }
}
