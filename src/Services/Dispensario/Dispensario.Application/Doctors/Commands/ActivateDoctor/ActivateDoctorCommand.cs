using BuildingBlocks.CQRS;
using FluentValidation;

namespace Dispensario.Application.Doctors.Commands.ActivateDoctor;

public sealed record ActivateDoctorCommand(Guid DoctorId) : ICommand;

public sealed class ActivateDoctorCommandValidator : AbstractValidator<ActivateDoctorCommand>
{
    public ActivateDoctorCommandValidator() => RuleFor(x => x.DoctorId).NotEmpty();
}
