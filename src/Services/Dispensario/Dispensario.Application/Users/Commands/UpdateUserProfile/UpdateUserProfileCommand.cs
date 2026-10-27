using BuildingBlocks.CQRS;
using Dispensario.Domain.Common;
using FluentValidation;

namespace Dispensario.Application.Users.Commands.UpdateUserProfile;

// Phone null borra el teléfono
public sealed record UpdateUserProfileCommand(Guid UserId, string FirstName, string LastName, string? Phone) : ICommand;

public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(FullName.MaxLength);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(FullName.MaxLength);
        RuleFor(x => x.Phone).MaximumLength(PhoneNumber.MaxLength).When(x => x.Phone is not null);
    }
}
