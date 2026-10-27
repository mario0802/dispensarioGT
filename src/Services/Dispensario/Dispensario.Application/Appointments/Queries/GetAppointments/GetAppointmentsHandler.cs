using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Appointments.Dtos;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Appointments.Queries.GetAppointments;

/// <summary>
/// Lista las citas paginadas y filtradas; un usuario Doctor solo ve las suyas.
/// </summary>
public sealed class GetAppointmentsHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IClinicTimeZone clinicTimeZone)
    : IQueryHandler<GetAppointmentsQuery, PaginatedResult<AppointmentDto>>
{
    public const int MaxPageSize = 100;

    public async Task<PaginatedResult<AppointmentDto>> Handle(GetAppointmentsQuery query, CancellationToken cancellationToken)
    {
        var doctorFilter = query.DoctorId;

        // Antes de leer la BD: un Doctor solo consulta su propia agenda
        if (currentUser.Role == Role.Doctor)
        {
            var ownDoctorId = currentUser.DoctorId?.Value
                ?? throw new ForbiddenException("Your user is not linked to a doctor.");

            if (doctorFilter is { } requested && requested != ownDoctorId)
                throw new ForbiddenException("You can only view your own appointments.");

            doctorFilter = ownDoctorId;
        }

        var (pageIndex, pageSize) = query.Pagination;

        // Las queries no pasan por ValidationBehavior: la paginación y el rango se validan aquí
        if (pageIndex < 0)
            throw new BadRequestException("PageIndex must be greater than or equal to 0.");

        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"PageSize must be between 1 and {MaxPageSize}.");

        if (query.From > query.To)
            throw new BadRequestException("From must be on or before To.");

        var source = db.Appointments.AsNoTracking();

        if (doctorFilter is { } doctorGuid)
        {
            var doctorId = new DoctorId(doctorGuid);
            source = source.Where(a => a.DoctorId == doctorId);
        }

        if (query.PatientId is { } patientGuid)
        {
            var patientId = new PatientId(patientGuid);
            source = source.Where(a => a.PatientId == patientId);
        }

        if (query.Status is { } status)
            source = source.Where(a => a.Status == status);

        // El rango de fechas es local: se convierte a límites UTC [desde 00:00, hasta el día siguiente 00:00)
        if (query.From is { } from)
        {
            var fromUtc = clinicTimeZone.ToUtc(from.ToDateTime(TimeOnly.MinValue));
            source = source.Where(a => a.StartUtc >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = clinicTimeZone.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
            source = source.Where(a => a.StartUtc < toUtc);
        }

        var count = await source.LongCountAsync(cancellationToken);

        var appointments = await source
            .OrderBy(a => a.StartUtc)
            .ThenBy(a => a.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToDto(db.Patients.AsNoTracking(), db.Doctors.AsNoTracking())
            .ToListAsync(cancellationToken);

        return new PaginatedResult<AppointmentDto>(
            pageIndex,
            pageSize,
            count,
            appointments.Select(a => a.WithLocalTimes(clinicTimeZone)).ToList());
    }
}
