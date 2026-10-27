using BuildingBlocks.CQRS;
using Dispensario.Application.Users.Validation;
using FluentValidation;

namespace Dispensario.Application.Users.Commands.ChangePassword;

// Cambio de la contraseña del usuario autenticado
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .ValidPassword()
            .NotEqual(x => x.CurrentPassword).WithMessage("'New Password' must be different from the current password.");
    }
}
