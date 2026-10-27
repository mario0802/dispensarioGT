using BuildingBlocks.CQRS;
using Dispensario.Domain.Consultations;
using FluentValidation;

namespace Dispensario.Application.Consultations.Commands.StartConsultation;

// El doctor no llega en el cuerpo: es el del usuario actual (ICurrentUser.DoctorId)
public sealed record StartConsultationCommand(
    Guid PatientId,
    Guid? AppointmentId,
    string Reason) : ICommand<StartConsultationResult>;

public sealed record StartConsultationResult(Guid Id);

public sealed class StartConsultationCommandValidator : AbstractValidator<StartConsultationCommand>
{
    public StartConsultationCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        // NotEmpty en un Guid? solo rechaza null: Guid.Empty se rechaza a mano y null (sin cita) pasa
        RuleFor(x => x.AppointmentId)
            .NotEqual(Guid.Empty)
            .WithMessage("'Appointment Id' must not be empty.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(Consultation.ReasonMaxLength);
    }
}
