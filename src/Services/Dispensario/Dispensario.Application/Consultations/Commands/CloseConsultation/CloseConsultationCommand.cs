using BuildingBlocks.CQRS;
using Dispensario.Domain.Consultations;
using FluentValidation;

namespace Dispensario.Application.Consultations.Commands.CloseConsultation;

// El diagnóstico es obligatorio al cerrar; Notes reemplaza las notas (null las borra)
public sealed record CloseConsultationCommand(
    Guid ConsultationId,
    string Diagnosis,
    string? Notes) : ICommand;

public sealed class CloseConsultationCommandValidator : AbstractValidator<CloseConsultationCommand>
{
    public CloseConsultationCommandValidator()
    {
        RuleFor(x => x.ConsultationId).NotEmpty();
        RuleFor(x => x.Diagnosis).NotEmpty().MaximumLength(Consultation.DiagnosisMaxLength);
        RuleFor(x => x.Notes).MaximumLength(Consultation.NotesMaxLength);
    }
}
