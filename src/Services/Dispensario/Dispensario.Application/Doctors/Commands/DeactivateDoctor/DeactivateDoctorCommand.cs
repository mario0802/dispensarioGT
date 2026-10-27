using BuildingBlocks.CQRS;
using FluentValidation;

namespace Dispensario.Application.Doctors.Commands.DeactivateDoctor;

public sealed record DeactivateDoctorCommand(Guid DoctorId) : ICommand;

public sealed class DeactivateDoctorCommandValidator : AbstractValidator<DeactivateDoctorCommand>
{
    public DeactivateDoctorCommandValidator() => RuleFor(x => x.DoctorId).NotEmpty();
}
