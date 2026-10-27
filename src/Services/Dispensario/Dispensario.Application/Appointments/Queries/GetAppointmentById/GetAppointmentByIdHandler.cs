using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Appointments.Dtos;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Appointments.Queries.GetAppointmentById;

/// <summary>
/// Devuelve una cita por su ID; un usuario Doctor solo puede ver las suyas.
/// </summary>
public sealed class GetAppointmentByIdHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IClinicTimeZone clinicTimeZone)
    : IQueryHandler<GetAppointmentByIdQuery, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(GetAppointmentByIdQuery query, CancellationToken cancellationToken)
    {
        var isDoctor = currentUser.Role == Role.Doctor;
        if (isDoctor && currentUser.DoctorId is null)
            throw new ForbiddenException("Your user is not linked to a doctor.");

        var appointmentId = new AppointmentId(query.AppointmentId);
        var appointment = await db.Appointments
            .AsNoTracking()
            .Where(a => a.Id == appointmentId)
            .ToDto(db.Patients.AsNoTracking(), db.Doctors.AsNoTracking())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Appointment), query.AppointmentId);

        // El 404 va antes: hace falta leer la cita para saber de qué doctor es
        if (isDoctor && appointment.DoctorId != currentUser.DoctorId!.Value.Value)
            throw new ForbiddenException("You can only view your own appointments.");

        return appointment.WithLocalTimes(clinicTimeZone);
    }
}
