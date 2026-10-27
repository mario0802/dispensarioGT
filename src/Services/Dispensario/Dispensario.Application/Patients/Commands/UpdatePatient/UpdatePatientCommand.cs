using BuildingBlocks.CQRS;
using Dispensario.Application.Patients.Commands.CreatePatient;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;
using FluentValidation;

namespace Dispensario.Application.Patients.Commands.UpdatePatient;

// Reemplaza todos los datos: un opcional null borra el valor actual. El documento también se puede cambiar
public sealed record UpdatePatientCommand(
    Guid PatientId,
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
    string? Allergies) : ICommand;

public sealed class UpdatePatientCommandValidator : AbstractValidator<UpdatePatientCommand>
{
    public UpdatePatientCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
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
