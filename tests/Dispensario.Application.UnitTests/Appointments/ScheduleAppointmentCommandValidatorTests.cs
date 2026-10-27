using Dispensario.Application.Appointments.Commands.ScheduleAppointment;
using Dispensario.Domain.Appointments;

namespace Dispensario.Application.UnitTests.Appointments;

public sealed class ScheduleAppointmentCommandValidatorTests
{
    private static readonly ScheduleAppointmentCommand ValidCommand = new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        new DateTime(2026, 11, 3, 9, 30, 0),
        "Control");

    private readonly ScheduleAppointmentCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_IsValid()
    {
        _validator.Validate(ValidCommand).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyIds_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { PatientId = Guid.Empty, DoctorId = Guid.Empty });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(ScheduleAppointmentCommand.PatientId));
        result.Errors.ShouldContain(error => error.PropertyName == nameof(ScheduleAppointmentCommand.DoctorId));
    }

    [Fact]
    public void Validate_WithDefaultStartLocal_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { StartLocal = default });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(ScheduleAppointmentCommand.StartLocal));
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void Validate_WithStartLocalCarryingOffset_IsInvalid(DateTimeKind kind)
    {
        var start = DateTime.SpecifyKind(ValidCommand.StartLocal, kind);

        var result = _validator.Validate(ValidCommand with { StartLocal = start });

        result.Errors.ShouldContain(error =>
            error.PropertyName == nameof(ScheduleAppointmentCommand.StartLocal)
            && error.ErrorMessage == "Start time must be a local time without offset.");
    }

    [Fact]
    public void Validate_WithEmptyReason_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { Reason = "" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(ScheduleAppointmentCommand.Reason));
    }

    [Fact]
    public void Validate_WithTooLongReason_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { Reason = new string('a', Appointment.ReasonMaxLength + 1) });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(ScheduleAppointmentCommand.Reason));
    }
}
