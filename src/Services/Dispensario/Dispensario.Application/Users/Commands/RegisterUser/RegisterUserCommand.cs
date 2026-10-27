using BuildingBlocks.CQRS;
using Dispensario.Application.Users.Validation;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using FluentValidation;

namespace Dispensario.Application.Users.Commands.RegisterUser;

// Lo usan el autoregistro (rol fijo Receptionist) y el alta por el Admin (cualquier rol)
public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    DocumentType DocumentType,
    string DocumentNumber,
    string? Phone,
    Role Role) : ICommand<RegisterUserResult>;

public sealed record RegisterUserResult(Guid Id);

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(Email.MaxLength).EmailAddress();
        RuleFor(x => x.Password).ValidPassword();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(FullName.MaxLength);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(FullName.MaxLength);
        RuleFor(x => x.DocumentType).IsInEnum();
        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(Document.NumberMaxLength);
        RuleFor(x => x.Phone).MaximumLength(PhoneNumber.MaxLength).When(x => x.Phone is not null);
        RuleFor(x => x.Role).IsInEnum();
    }
}
