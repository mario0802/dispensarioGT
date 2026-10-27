using System.Globalization;
using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Doctors.Dtos;
using Dispensario.Domain.Doctors;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Doctors.Queries.GetDoctorSchedule;

/// <summary>
/// Devuelve el horario semanal de un doctor (también si está inactivo), de lunes a domingo.
/// </summary>
public sealed class GetDoctorScheduleHandler(IApplicationDbContext db) : IQueryHandler<GetDoctorScheduleQuery, DoctorScheduleDto>
{
    public async Task<DoctorScheduleDto> Handle(GetDoctorScheduleQuery query, CancellationToken cancellationToken)
    {
        var doctorId = new DoctorId(query.DoctorId);

        var doctor = await db.Doctors.AsNoTracking().SingleOrDefaultAsync(d => d.Id == doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Doctor), query.DoctorId);

        // En memoria: son pocos bloques de un solo doctor. DayOfWeek empieza en domingo (0); se desplaza para empezar en lunes
        var blocks = doctor.Schedule
            .OrderBy(block => ((int)block.DayOfWeek + 6) % 7)
            .ThenBy(block => block.StartTime)
            .Select(block => new ScheduleBlockDto(block.DayOfWeek, FormatTime(block.StartTime), FormatTime(block.EndTime)))
            .ToList();

        return new DoctorScheduleDto(query.DoctorId, blocks);
    }

    private static string FormatTime(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
