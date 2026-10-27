using Dispensario.Application.Consultations.Commands.CloseConsultation;
using Dispensario.Domain.Consultations;

namespace Dispensario.Application.UnitTests.Consultations;

public sealed class CloseConsultationCommandValidatorTests
{
    private static readonly CloseConsultationCommand ValidCommand =
        new(Guid.CreateVersion7(), "Cefalea tensional", "Control en dos semanas.");

    private readonly CloseConsultationCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_IsValid()
    {
        _validator.Validate(ValidCommand).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNullNotes_IsValid()
    {
        _validator.Validate(ValidCommand with { Notes = null }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyId_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { ConsultationId = Guid.Empty });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CloseConsultationCommand.ConsultationId));
    }

    [Fact]
    public void Validate_WithEmptyDiagnosis_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { Diagnosis = "" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CloseConsultationCommand.Diagnosis));
    }

    [Fact]
    public void Validate_WithTooLongDiagnosis_IsInvalid()
    {
        var result = _validator.Validate(
            ValidCommand with { Diagnosis = new string('a', Consultation.DiagnosisMaxLength + 1) });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CloseConsultationCommand.Diagnosis));
    }

    [Fact]
    public void Validate_WithTooLongNotes_IsInvalid()
    {
        var result = _validator.Validate(
            ValidCommand with { Notes = new string('a', Consultation.NotesMaxLength + 1) });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CloseConsultationCommand.Notes));
    }
}
