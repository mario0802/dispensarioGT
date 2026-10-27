using BuildingBlocks.CQRS;
using FluentValidation;

namespace Dispensario.Application.Users.Commands.LinkDoctor;

public sealed record LinkDoctorCommand(Guid UserId, Guid DoctorId) : ICommand;

public sealed class LinkDoctorCommandValidator : AbstractValidator<LinkDoctorCommand>
{
    public LinkDoctorCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DoctorId).NotEmpty();
    }
}
