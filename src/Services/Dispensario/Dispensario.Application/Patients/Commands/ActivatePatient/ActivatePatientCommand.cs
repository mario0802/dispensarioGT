using BuildingBlocks.CQRS;
using FluentValidation;

namespace Dispensario.Application.Patients.Commands.ActivatePatient;

public sealed record ActivatePatientCommand(Guid PatientId) : ICommand;

public sealed class ActivatePatientCommandValidator : AbstractValidator<ActivatePatientCommand>
{
    public ActivatePatientCommandValidator() => RuleFor(x => x.PatientId).NotEmpty();
}
