using Dispensario.Application.Appointments.Commands.RescheduleAppointment;

namespace Dispensario.Application.UnitTests.Appointments;

public sealed class RescheduleAppointmentCommandValidatorTests
{
    private static readonly RescheduleAppointmentCommand ValidCommand = new(
        Guid.CreateVersion7(),
        new DateTime(2026, 11, 4, 10, 0, 0));

    private readonly RescheduleAppointmentCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_IsValid()
    {
        _validator.Validate(ValidCommand).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyId_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { AppointmentId = Guid.Empty });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(RescheduleAppointmentCommand.AppointmentId));
    }

    [Fact]
    public void Validate_WithDefaultStartLocal_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { StartLocal = default });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(RescheduleAppointmentCommand.StartLocal));
    }

    [Fact]
    public void Validate_WithUtcStartLocal_IsInvalid()
    {
        var start = DateTime.SpecifyKind(ValidCommand.StartLocal, DateTimeKind.Utc);

        var result = _validator.Validate(ValidCommand with { StartLocal = start });

        result.Errors.ShouldContain(error =>
            error.PropertyName == nameof(RescheduleAppointmentCommand.StartLocal)
            && error.ErrorMessage == "Start time must be a local time without offset.");
    }
}
