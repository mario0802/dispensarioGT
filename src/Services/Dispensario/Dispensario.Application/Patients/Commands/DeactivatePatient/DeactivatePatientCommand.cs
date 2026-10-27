using BuildingBlocks.CQRS;
using FluentValidation;

namespace Dispensario.Application.Patients.Commands.DeactivatePatient;

public sealed record DeactivatePatientCommand(Guid PatientId) : ICommand;

public sealed class DeactivatePatientCommandValidator : AbstractValidator<DeactivatePatientCommand>
{
    public DeactivatePatientCommandValidator() => RuleFor(x => x.PatientId).NotEmpty();
}
