using BuildingBlocks.CQRS;
using Dispensario.Domain.Appointments;
using FluentValidation;

namespace Dispensario.Application.Appointments.Commands.CancelAppointment;

public sealed record CancelAppointmentCommand(Guid AppointmentId, string Reason) : ICommand;

public sealed class CancelAppointmentCommandValidator : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(Appointment.CancellationReasonMaxLength);
    }
}
