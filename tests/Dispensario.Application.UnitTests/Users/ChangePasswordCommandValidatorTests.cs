using Dispensario.Application.Users.Commands.ChangePassword;

namespace Dispensario.Application.UnitTests.Users;

public sealed class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_IsValid()
    {
        var result = _validator.Validate(new ChangePasswordCommand("OldPassw0rd", "NewPassw0rd"));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithSamePassword_HasNewPasswordError()
    {
        var result = _validator.Validate(new ChangePasswordCommand("Passw0rd1", "Passw0rd1"));

        result.Errors.ShouldContain(error => error.PropertyName == nameof(ChangePasswordCommand.NewPassword));
    }

    [Fact]
    public void Validate_WithWeakNewPassword_HasNewPasswordError()
    {
        var result = _validator.Validate(new ChangePasswordCommand("OldPassw0rd", "short1"));

        result.Errors.ShouldContain(error => error.PropertyName == nameof(ChangePasswordCommand.NewPassword));
    }

    [Fact]
    public void Validate_WithEmptyCurrentPassword_HasCurrentPasswordError()
    {
        var result = _validator.Validate(new ChangePasswordCommand("", "NewPassw0rd"));

        result.Errors.ShouldContain(error => error.PropertyName == nameof(ChangePasswordCommand.CurrentPassword));
    }
}
