using Dispensario.Application.Abstractions;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Consultations.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dispensario.Application.Appointments.EventHandlers;

/// <summary>
/// Completa la cita enlazada a una consulta cerrada; corre después del commit de la consulta.
/// </summary>
public sealed class ConsultationClosedHandler(
    IApplicationDbContext db,
    ILogger<ConsultationClosedHandler> logger)
    : INotificationHandler<ConsultationClosedDomainEvent>
{
    public async Task Handle(ConsultationClosedDomainEvent notification, CancellationToken cancellationToken)
    {
        // Consulta sin cita: no hay nada que completar
        if (notification.AppointmentId is not { } appointmentId)
            return;

        var appointment = await db.Appointments.SingleOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);

        // La consulta ya está guardada: fallar aquí no la desharía, así que solo se deja rastro en el log
        if (appointment is null)
        {
            logger.LogWarning(
                "Appointment {AppointmentId} of closed consultation {ConsultationId} was not found.",
                appointmentId.Value,
                notification.ConsultationId.Value);
            return;
        }

        if (appointment.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
        {
            logger.LogWarning(
                "Appointment {AppointmentId} of closed consultation {ConsultationId} is {Status} and was not completed.",
                appointmentId.Value,
                notification.ConsultationId.Value,
                appointment.Status);
            return;
        }

        appointment.Complete();
        await db.SaveChangesAsync(cancellationToken);
    }
}
