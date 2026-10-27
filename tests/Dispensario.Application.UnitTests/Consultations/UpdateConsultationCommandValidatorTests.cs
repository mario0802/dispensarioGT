using Dispensario.Application.Consultations.Commands.UpdateConsultation;
using Dispensario.Domain.Consultations;

namespace Dispensario.Application.UnitTests.Consultations;

public sealed class UpdateConsultationCommandValidatorTests
{
    private static readonly UpdateConsultationCommand ValidCommand =
        new(Guid.CreateVersion7(), "Dolor de cabeza", "Cefalea tensional", "Sin signos de alarma.");

    private readonly UpdateConsultationCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_IsValid()
    {
        _validator.Validate(ValidCommand).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNullOptionals_IsValid()
    {
        _validator.Validate(ValidCommand with { Diagnosis = null, Notes = null }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyId_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { ConsultationId = Guid.Empty });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(UpdateConsultationCommand.ConsultationId));
    }

    [Fact]
    public void Validate_WithEmptyReason_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { Reason = "" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(UpdateConsultationCommand.Reason));
    }

    [Fact]
    public void Validate_WithTooLongDiagnosis_IsInvalid()
    {
        var result = _validator.Validate(
            ValidCommand with { Diagnosis = new string('a', Consultation.DiagnosisMaxLength + 1) });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(UpdateConsultationCommand.Diagnosis));
    }

    [Fact]
    public void Validate_WithTooLongNotes_IsInvalid()
    {
        var result = _validator.Validate(
            ValidCommand with { Notes = new string('a', Consultation.NotesMaxLength + 1) });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(UpdateConsultationCommand.Notes));
    }
}
