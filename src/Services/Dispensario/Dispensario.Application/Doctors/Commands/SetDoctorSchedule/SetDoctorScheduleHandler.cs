using System.Globalization;
using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Doctors.Commands.SetDoctorSchedule;

/// <summary>
/// Reemplaza el horario semanal de un doctor; lo hace el Admin o el propio doctor.
/// </summary>
public sealed class SetDoctorScheduleHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : ICommandHandler<SetDoctorScheduleCommand>
{
    public async Task<Unit> Handle(SetDoctorScheduleCommand command, CancellationToken cancellationToken)
    {
        var doctorId = new DoctorId(command.DoctorId);

        // Antes de leer la BD: un Doctor no debe averiguar si existe el ID de otro doctor
        if (currentUser.Role == Role.Doctor && currentUser.DoctorId != doctorId)
            throw new ForbiddenException("You can only change your own schedule.");

        var doctor = await db.Doctors.SingleOrDefaultAsync(d => d.Id == doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Doctor), command.DoctorId);

        // El validador ya garantiza el formato "HH:mm"; las reglas del horario lanzan DomainException (400)
        var blocks = command.Blocks
            .Select(block => ScheduleBlock.Create(block.DayOfWeek, ParseTime(block.StartTime), ParseTime(block.EndTime)))
            .ToList();

        doctor.SetWeeklySchedule(blocks);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }

    private static TimeOnly ParseTime(string value) => TimeOnly.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture);
}
