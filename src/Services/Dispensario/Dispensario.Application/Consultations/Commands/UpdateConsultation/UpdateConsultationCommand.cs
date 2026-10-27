using BuildingBlocks.CQRS;
using Dispensario.Domain.Consultations;
using FluentValidation;

namespace Dispensario.Application.Consultations.Commands.UpdateConsultation;

// Reemplaza los tres textos: un opcional null borra el valor
public sealed record UpdateConsultationCommand(
    Guid ConsultationId,
    string Reason,
    string? Diagnosis,
    string? Notes) : ICommand;

public sealed class UpdateConsultationCommandValidator : AbstractValidator<UpdateConsultationCommand>
{
    public UpdateConsultationCommandValidator()
    {
        RuleFor(x => x.ConsultationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(Consultation.ReasonMaxLength);
        RuleFor(x => x.Diagnosis).MaximumLength(Consultation.DiagnosisMaxLength);
        RuleFor(x => x.Notes).MaximumLength(Consultation.NotesMaxLength);
    }
}
