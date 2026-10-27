using BuildingBlocks.CQRS;
using Dispensario.Domain.Appointments;
using FluentValidation;

namespace Dispensario.Application.Appointments.Commands.ScheduleAppointment;

// StartLocal es la hora local del dispensario (Clinic:TimeZone), sin offset
public sealed record ScheduleAppointmentCommand(
    Guid PatientId,
    Guid DoctorId,
    DateTime StartLocal,
    string Reason) : ICommand<ScheduleAppointmentResult>;

public sealed record ScheduleAppointmentResult(Guid Id);

public sealed class ScheduleAppointmentCommandValidator : AbstractValidator<ScheduleAppointmentCommand>
{
    public ScheduleAppointmentCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.DoctorId).NotEmpty();

        // System.Text.Json solo deja Kind = Unspecified cuando el texto no trae "Z" ni offset
        RuleFor(x => x.StartLocal)
            .NotEmpty()
            .Must(value => value.Kind == DateTimeKind.Unspecified)
            .WithMessage("Start time must be a local time without offset.");

        RuleFor(x => x.Reason).NotEmpty().MaximumLength(Appointment.ReasonMaxLength);
    }
}
