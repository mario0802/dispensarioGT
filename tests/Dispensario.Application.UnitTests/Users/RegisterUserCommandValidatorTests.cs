using Dispensario.Application.Users.Commands.RegisterUser;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;

namespace Dispensario.Application.UnitTests.Users;

public sealed class RegisterUserCommandValidatorTests
{
    private static readonly RegisterUserCommand ValidCommand = new(
        "ana.perez@dispensario.local",
        "Passw0rd",
        "Ana",
        "Pérez",
        DocumentType.NationalId,
        "1712345678",
        "+593 99 123 4567",
        Role.Receptionist);

    private readonly RegisterUserCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_IsValid()
    {
        var result = _validator.Validate(ValidCommand);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithoutPhone_IsValid()
    {
        var result = _validator.Validate(ValidCommand with { Phone = null });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithInvalidEmail_HasEmailError()
    {
        var result = _validator.Validate(ValidCommand with { Email = "not-an-email" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterUserCommand.Email));
    }

    [Fact]
    public void Validate_WithWeakPassword_HasPasswordError()
    {
        var result = _validator.Validate(ValidCommand with { Password = "password" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterUserCommand.Password));
    }

    [Fact]
    public void Validate_WithEmptyFirstName_HasFirstNameError()
    {
        var result = _validator.Validate(ValidCommand with { FirstName = "" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterUserCommand.FirstName));
    }

    [Fact]
    public void Validate_WithTooLongDocumentNumber_HasDocumentNumberError()
    {
        var result = _validator.Validate(ValidCommand with { DocumentNumber = new string('1', Document.NumberMaxLength + 1) });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterUserCommand.DocumentNumber));
    }

    [Fact]
    public void Validate_WithUndefinedRole_HasRoleError()
    {
        var result = _validator.Validate(ValidCommand with { Role = (Role)99 });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterUserCommand.Role));
    }

    [Fact]
    public void Validate_WithUndefinedDocumentType_HasDocumentTypeError()
    {
        var result = _validator.Validate(ValidCommand with { DocumentType = (DocumentType)99 });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterUserCommand.DocumentType));
    }
}
