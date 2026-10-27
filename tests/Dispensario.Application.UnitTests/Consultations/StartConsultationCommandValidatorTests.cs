using Dispensario.Application.Consultations.Commands.StartConsultation;
using Dispensario.Domain.Consultations;

namespace Dispensario.Application.UnitTests.Consultations;

public sealed class StartConsultationCommandValidatorTests
{
    private static readonly StartConsultationCommand ValidCommand =
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Dolor de cabeza");

    private readonly StartConsultationCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_IsValid()
    {
        _validator.Validate(ValidCommand).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithoutAppointment_IsValid()
    {
        _validator.Validate(ValidCommand with { AppointmentId = null }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyPatientId_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { PatientId = Guid.Empty });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(StartConsultationCommand.PatientId));
    }

    [Fact]
    public void Validate_WithEmptyAppointmentId_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { AppointmentId = Guid.Empty });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(StartConsultationCommand.AppointmentId));
    }

    [Fact]
    public void Validate_WithEmptyReason_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { Reason = "" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(StartConsultationCommand.Reason));
    }

    [Fact]
    public void Validate_WithTooLongReason_IsInvalid()
    {
        var result = _validator.Validate(
            ValidCommand with { Reason = new string('a', Consultation.ReasonMaxLength + 1) });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(StartConsultationCommand.Reason));
    }
}
