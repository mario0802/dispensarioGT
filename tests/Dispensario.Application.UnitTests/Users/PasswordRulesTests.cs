using Dispensario.Application.Users.Validation;
using FluentValidation;

namespace Dispensario.Application.UnitTests.Users;

public sealed class PasswordRulesTests
{
    private readonly PasswordValidator _validator = new();

    [Theory]
    [InlineData("Abcdefg1")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopq123")]
    public void ValidPassword_WithValidPassword_IsValid(string password)
    {
        var result = _validator.Validate(new PasswordHolder(password));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Abcdef1")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopq1234")]
    [InlineData("Abcdefgh")]
    [InlineData("12345678")]
    public void ValidPassword_WithInvalidPassword_IsInvalid(string password)
    {
        var result = _validator.Validate(new PasswordHolder(password));

        result.IsValid.ShouldBeFalse();
    }

    // Validador mínimo para probar la regla aislada
    private sealed record PasswordHolder(string Password);

    private sealed class PasswordValidator : AbstractValidator<PasswordHolder>
    {
        public PasswordValidator() => RuleFor(x => x.Password).ValidPassword();
    }
}
