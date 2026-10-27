using Dispensario.Application.Appointments.Commands.CancelAppointment;
using Dispensario.Domain.Appointments;

namespace Dispensario.Application.UnitTests.Appointments;

public sealed class CancelAppointmentCommandValidatorTests
{
    private static readonly CancelAppointmentCommand ValidCommand = new(Guid.CreateVersion7(), "Viaje del paciente");

    private readonly CancelAppointmentCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_IsValid()
    {
        _validator.Validate(ValidCommand).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyId_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { AppointmentId = Guid.Empty });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CancelAppointmentCommand.AppointmentId));
    }

    [Fact]
    public void Validate_WithEmptyReason_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { Reason = "" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CancelAppointmentCommand.Reason));
    }

    [Fact]
    public void Validate_WithTooLongReason_IsInvalid()
    {
        var result = _validator.Validate(
            ValidCommand with { Reason = new string('a', Appointment.CancellationReasonMaxLength + 1) });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CancelAppointmentCommand.Reason));
    }
}
