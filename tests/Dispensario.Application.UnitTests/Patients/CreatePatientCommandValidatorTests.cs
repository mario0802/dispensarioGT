using Dispensario.Application.Patients.Commands.CreatePatient;
using Dispensario.Application.Patients.Commands.UpdatePatient;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;

namespace Dispensario.Application.UnitTests.Patients;

public sealed class CreatePatientCommandValidatorTests
{
    private static readonly CreatePatientCommand MinimalCommand = new(
        DocumentType.NationalId,
        "0102030405",
        "Ana",
        "Pérez",
        new DateOnly(1990, 5, 14),
        Gender.Female,
        null,
        null,
        null,
        null,
        null);

    private static readonly CreatePatientCommand FullCommand = MinimalCommand with
    {
        Email = "ana@mail.com",
        Phone = "0991234567",
        Address = new AddressInput("Av. Amazonas 123", "Quito", "Pichincha", "Frente al parque"),
        BloodType = "AB-",
        Allergies = "Penicillin",
    };

    private readonly CreatePatientCommandValidator _validator = new();

    [Fact]
    public void Validate_WithMinimalCommand_IsValid()
    {
        _validator.Validate(MinimalCommand).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithFullCommand_IsValid()
    {
        _validator.Validate(FullCommand).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789012345678901")]
    public void Validate_WithInvalidDocumentNumber_HasDocumentNumberError(string number)
    {
        var result = _validator.Validate(MinimalCommand with { DocumentNumber = number });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreatePatientCommand.DocumentNumber));
    }

    [Fact]
    public void Validate_WithEmptyFirstName_HasFirstNameError()
    {
        var result = _validator.Validate(MinimalCommand with { FirstName = "" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreatePatientCommand.FirstName));
    }

    [Fact]
    public void Validate_WithUndefinedGender_HasGenderError()
    {
        var result = _validator.Validate(MinimalCommand with { Gender = (Gender)99 });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreatePatientCommand.Gender));
    }

    [Fact]
    public void Validate_WithInvalidEmail_HasEmailError()
    {
        var result = _validator.Validate(MinimalCommand with { Email = "not-an-email" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreatePatientCommand.Email));
    }

    [Theory]
    [InlineData("", "Quito", "Address.Street")]
    [InlineData("Av. Amazonas 123", "", "Address.City")]
    public void Validate_WithIncompleteAddress_HasAddressError(string street, string city, string property)
    {
        var result = _validator.Validate(MinimalCommand with { Address = new AddressInput(street, city, null, null) });

        result.Errors.ShouldContain(error => error.PropertyName == property);
    }

    [Fact]
    public void Validate_WithUnknownBloodType_HasBloodTypeError()
    {
        var result = _validator.Validate(MinimalCommand with { BloodType = "X+" });

        result.Errors.ShouldContain(error =>
            error.PropertyName == nameof(CreatePatientCommand.BloodType) &&
            error.ErrorMessage == "Blood type must be one of A+, A-, B+, B-, AB+, AB-, O+, O-.");
    }

    [Fact]
    public void Validate_WithTooLongAllergies_HasAllergiesError()
    {
        var result = _validator.Validate(MinimalCommand with { Allergies = new string('a', Patient.AllergiesMaxLength + 1) });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreatePatientCommand.Allergies));
    }

    [Fact]
    public void UpdateValidator_WithValidCommand_IsValid()
    {
        var command = UpdateCommand(Guid.NewGuid()) with
        {
            Email = "ana@mail.com",
            Address = new AddressInput("Calle 10", "Cuenca", null, null),
            BloodType = "O+",
        };

        new UpdatePatientCommandValidator().Validate(command).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void UpdateValidator_WithEmptyPatientId_HasPatientIdError()
    {
        var result = new UpdatePatientCommandValidator().Validate(UpdateCommand(Guid.Empty));

        result.Errors.ShouldContain(error => error.PropertyName == nameof(UpdatePatientCommand.PatientId));
    }

    [Fact]
    public void UpdateValidator_WithUnknownBloodType_HasBloodTypeError()
    {
        var result = new UpdatePatientCommandValidator().Validate(UpdateCommand(Guid.NewGuid()) with { BloodType = "o+" });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(UpdatePatientCommand.BloodType));
    }

    private static UpdatePatientCommand UpdateCommand(Guid patientId) => new(
        patientId, DocumentType.Passport, "AB123", "Ana", "Pérez", new DateOnly(1990, 5, 14), Gender.Female,
        null, null, null, null, null);
}
