using Dispensario.Application.Doctors.Commands.CreateDoctor;
using Dispensario.Application.Doctors.Commands.UpdateDoctor;

namespace Dispensario.Application.UnitTests.Doctors;

public sealed class CreateDoctorCommandValidatorTests
{
    private static readonly CreateDoctorCommand ValidCommand = new(
        "Luis",
        "Mora",
        "MP-123",
        "Pediatría",
        "luis.mora@dispensario.local",
        "0991234567",
        30);

    private readonly CreateDoctorCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_IsValid()
    {
        var result = _validator.Validate(ValidCommand);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithoutDurationAndPhone_IsValid()
    {
        var result = _validator.Validate(ValidCommand with { AppointmentDurationMinutes = null, Phone = null });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyFirstName_HasFirstNameError()
    {
        var result = _validator.Validate(ValidCommand with { FirstName = "" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateDoctorCommand.FirstName));
    }

    [Fact]
    public void Validate_WithInvalidEmail_HasEmailError()
    {
        var result = _validator.Validate(ValidCommand with { Email = "not-an-email" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateDoctorCommand.Email));
    }

    [Fact]
    public void Validate_WithTooLongLicenseNumber_HasLicenseNumberError()
    {
        var result = _validator.Validate(ValidCommand with { LicenseNumber = new string('A', 31) });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateDoctorCommand.LicenseNumber));
    }

    [Fact]
    public void Validate_WithTooLongSpecialty_HasSpecialtyError()
    {
        var result = _validator.Validate(ValidCommand with { Specialty = new string('a', 101) });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateDoctorCommand.Specialty));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(125)]
    [InlineData(32)]
    public void Validate_WithInvalidDuration_HasDurationError(int minutes)
    {
        var result = _validator.Validate(ValidCommand with { AppointmentDurationMinutes = minutes });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateDoctorCommand.AppointmentDurationMinutes));
    }

    [Fact]
    public void UpdateValidator_WithEmptyDoctorId_HasDoctorIdError()
    {
        var command = new UpdateDoctorCommand(Guid.Empty, "Luis", "Mora", "MP-123", "Pediatría", "luis@dispensario.local", null, null);

        var result = new UpdateDoctorCommandValidator().Validate(command);

        result.Errors.ShouldContain(error => error.PropertyName == nameof(UpdateDoctorCommand.DoctorId));
    }

    [Fact]
    public void UpdateValidator_WithInvalidDuration_HasDurationError()
    {
        var command = new UpdateDoctorCommand(Guid.NewGuid(), "Luis", "Mora", "MP-123", "Pediatría", "luis@dispensario.local", null, 32);

        var result = new UpdateDoctorCommandValidator().Validate(command);

        result.Errors.ShouldContain(error => error.PropertyName == nameof(UpdateDoctorCommand.AppointmentDurationMinutes));
    }

    [Fact]
    public void UpdateValidator_WithValidCommandWithoutDuration_IsValid()
    {
        var command = new UpdateDoctorCommand(Guid.NewGuid(), "Luis", "Mora", "MP-123", "Pediatría", "luis@dispensario.local", null, null);

        var result = new UpdateDoctorCommandValidator().Validate(command);

        result.IsValid.ShouldBeTrue();
    }
}
