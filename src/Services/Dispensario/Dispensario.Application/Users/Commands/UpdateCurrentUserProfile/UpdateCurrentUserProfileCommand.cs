using BuildingBlocks.CQRS;
using Dispensario.Domain.Common;
using FluentValidation;

namespace Dispensario.Application.Users.Commands.UpdateCurrentUserProfile;

// Phone null borra el teléfono
public sealed record UpdateCurrentUserProfileCommand(string FirstName, string LastName, string? Phone) : ICommand;

public sealed class UpdateCurrentUserProfileCommandValidator : AbstractValidator<UpdateCurrentUserProfileCommand>
{
    public UpdateCurrentUserProfileCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(FullName.MaxLength);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(FullName.MaxLength);
        RuleFor(x => x.Phone).MaximumLength(PhoneNumber.MaxLength).When(x => x.Phone is not null);
    }
}
