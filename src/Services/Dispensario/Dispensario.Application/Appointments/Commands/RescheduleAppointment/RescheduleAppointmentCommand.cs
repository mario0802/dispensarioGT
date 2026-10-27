using BuildingBlocks.CQRS;
using FluentValidation;

namespace Dispensario.Application.Appointments.Commands.RescheduleAppointment;

// StartLocal es la nueva hora local del dispensario (Clinic:TimeZone), sin offset
public sealed record RescheduleAppointmentCommand(Guid AppointmentId, DateTime StartLocal) : ICommand;

public sealed class RescheduleAppointmentCommandValidator : AbstractValidator<RescheduleAppointmentCommand>
{
    public RescheduleAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();

        // System.Text.Json solo deja Kind = Unspecified cuando el texto no trae "Z" ni offset
        RuleFor(x => x.StartLocal)
            .NotEmpty()
            .Must(value => value.Kind == DateTimeKind.Unspecified)
            .WithMessage("Start time must be a local time without offset.");
    }
}
