using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Appointments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Appointments.Commands.ConfirmAppointment;

/// <summary>
/// Confirma una cita agendada antes de su hora de inicio.
/// </summary>
public sealed class ConfirmAppointmentHandler(IApplicationDbContext db, TimeProvider timeProvider)
    : ICommandHandler<ConfirmAppointmentCommand>
{
    public async Task<Unit> Handle(ConfirmAppointmentCommand command, CancellationToken cancellationToken)
    {
        var appointmentId = new AppointmentId(command.AppointmentId);

        var appointment = await db.Appointments.SingleOrDefaultAsync(a => a.Id == appointmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Appointment), command.AppointmentId);

        // Estado o momento no válidos: el dominio lanza DomainException (400)
        appointment.Confirm(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
