using BuildingBlocks.CQRS;
using FluentValidation;

namespace Dispensario.Application.Users.Commands.ActivateUser;

public sealed record ActivateUserCommand(Guid UserId) : ICommand;

public sealed class ActivateUserCommandValidator : AbstractValidator<ActivateUserCommand>
{
    public ActivateUserCommandValidator() => RuleFor(x => x.UserId).NotEmpty();
}
