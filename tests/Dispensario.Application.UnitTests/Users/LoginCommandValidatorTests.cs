using Dispensario.Application.Users.Commands.Login;

namespace Dispensario.Application.UnitTests.Users;

public sealed class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_IsValid()
    {
        var result = _validator.Validate(new LoginCommand("admin@dispensario.local", "secret"));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_WithInvalidEmail_HasEmailError(string email)
    {
        var result = _validator.Validate(new LoginCommand(email, "secret"));

        result.Errors.ShouldContain(error => error.PropertyName == nameof(LoginCommand.Email));
    }

    [Fact]
    public void Validate_WithEmptyPassword_HasPasswordError()
    {
        var result = _validator.Validate(new LoginCommand("admin@dispensario.local", ""));

        result.Errors.ShouldContain(error => error.PropertyName == nameof(LoginCommand.Password));
    }
}
