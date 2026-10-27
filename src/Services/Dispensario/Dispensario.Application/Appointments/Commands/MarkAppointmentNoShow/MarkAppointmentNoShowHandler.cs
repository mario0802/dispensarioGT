using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Appointments.Commands.MarkAppointmentNoShow;

/// <summary>
/// Marca como no asistida una cita ya empezada; un usuario Doctor solo puede marcar las suyas.
/// </summary>
public sealed class MarkAppointmentNoShowHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<MarkAppointmentNoShowCommand>
{
    public async Task<Unit> Handle(MarkAppointmentNoShowCommand command, CancellationToken cancellationToken)
    {
        var isDoctor = currentUser.Role == Role.Doctor;
        if (isDoctor && currentUser.DoctorId is null)
            throw new ForbiddenException("Your user is not linked to a doctor.");

        var appointmentId = new AppointmentId(command.AppointmentId);
        var appointment = await db.Appointments.SingleOrDefaultAsync(a => a.Id == appointmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Appointment), command.AppointmentId);

        // El 404 va antes: hace falta leer la cita para saber de qué doctor es
        if (isDoctor && appointment.DoctorId != currentUser.DoctorId)
            throw new ForbiddenException("You can only manage your own appointments.");

        appointment.MarkNoShow(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
