using BuildingBlocks.CQRS;
using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;
using FluentValidation;

namespace Dispensario.Application.Doctors.Commands.UpdateDoctor;

// Phone null borra el teléfono; AppointmentDurationMinutes null conserva la duración actual
public sealed record UpdateDoctorCommand(
    Guid DoctorId,
    string FirstName,
    string LastName,
    string LicenseNumber,
    string Specialty,
    string Email,
    string? Phone,
    int? AppointmentDurationMinutes) : ICommand;

public sealed class UpdateDoctorCommandValidator : AbstractValidator<UpdateDoctorCommand>
{
    public UpdateDoctorCommandValidator()
    {
        RuleFor(x => x.DoctorId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(FullName.MaxLength);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(FullName.MaxLength);
        RuleFor(x => x.LicenseNumber).NotEmpty().MaximumLength(Doctor.LicenseNumberMaxLength);
        RuleFor(x => x.Specialty).NotEmpty().MaximumLength(Doctor.SpecialtyMaxLength);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(Email.MaxLength).EmailAddress();
        RuleFor(x => x.Phone).MaximumLength(PhoneNumber.MaxLength).When(x => x.Phone is not null);
        RuleFor(x => x.AppointmentDurationMinutes)
            .InclusiveBetween(Doctor.MinAppointmentDurationMinutes, Doctor.MaxAppointmentDurationMinutes)
            .Must(minutes => minutes % Doctor.AppointmentDurationStepMinutes == 0)
            .WithMessage($"'{{PropertyName}}' must be a multiple of {Doctor.AppointmentDurationStepMinutes}.")
            .When(x => x.AppointmentDurationMinutes is not null);
    }
}
