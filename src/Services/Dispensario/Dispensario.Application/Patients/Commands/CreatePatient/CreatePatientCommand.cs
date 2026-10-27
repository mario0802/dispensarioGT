using BuildingBlocks.CQRS;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;
using FluentValidation;

namespace Dispensario.Application.Patients.Commands.CreatePatient;

// BloodType en notación clínica ("A+", "O-"…) o null; Address null = sin dirección
public sealed record CreatePatientCommand(
    DocumentType DocumentType,
    string DocumentNumber,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    Gender Gender,
    string? Email,
    string? Phone,
    AddressInput? Address,
    string? BloodType,
    string? Allergies) : ICommand<CreatePatientResult>;

public sealed record AddressInput(string Street, string City, string? State, string? Reference);

public sealed record CreatePatientResult(Guid Id);

public sealed class CreatePatientCommandValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientCommandValidator()
    {
        RuleFor(x => x.DocumentType).IsInEnum();
        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(Document.NumberMaxLength);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(FullName.MaxLength);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(FullName.MaxLength);
        RuleFor(x => x.BirthDate).NotEmpty();
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.Email!).MaximumLength(Email.MaxLength).EmailAddress().When(x => x.Email is not null);
        RuleFor(x => x.Phone).MaximumLength(PhoneNumber.MaxLength).When(x => x.Phone is not null);
        RuleFor(x => x.Address!).SetValidator(new AddressInputValidator()).When(x => x.Address is not null);
        RuleFor(x => x.BloodType)
            .Must(value => BloodTypeNotation.TryParse(value, out _))
            .WithMessage(BloodTypeNotation.InvalidMessage)
            .When(x => x.BloodType is not null);
        RuleFor(x => x.Allergies).MaximumLength(Patient.AllergiesMaxLength).When(x => x.Allergies is not null);
    }
}

public sealed class AddressInputValidator : AbstractValidator<AddressInput>
{
    public AddressInputValidator()
    {
        RuleFor(x => x.Street).NotEmpty().MaximumLength(Address.StreetMaxLength);
        RuleFor(x => x.City).NotEmpty().MaximumLength(Address.CityMaxLength);
        RuleFor(x => x.State).MaximumLength(Address.StateMaxLength).When(x => x.State is not null);
        RuleFor(x => x.Reference).MaximumLength(Address.ReferenceMaxLength).When(x => x.Reference is not null);
    }
}
