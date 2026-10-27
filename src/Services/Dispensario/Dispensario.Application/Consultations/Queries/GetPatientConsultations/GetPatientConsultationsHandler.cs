using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Consultations.Dtos;
using Dispensario.Domain.Patients;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Consultations.Queries.GetPatientConsultations;

/// <summary>
/// Devuelve la historia clínica paginada de un paciente; la leen el Admin y cualquier doctor vinculado.
/// </summary>
public sealed class GetPatientConsultationsHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IClinicTimeZone clinicTimeZone)
    : IQueryHandler<GetPatientConsultationsQuery, PaginatedResult<ConsultationDto>>
{
    public const int MaxPageSize = 100;

    public async Task<PaginatedResult<ConsultationDto>> Handle(
        GetPatientConsultationsQuery query,
        CancellationToken cancellationToken)
    {
        // Antes de leer la BD; el Admin no necesita vínculo
        if (currentUser.Role == Role.Doctor && currentUser.DoctorId is null)
            throw new ForbiddenException("Your user is not linked to a doctor.");

        var (pageIndex, pageSize) = query.Pagination;

        // Las queries no pasan por ValidationBehavior: la paginación se valida aquí
        if (pageIndex < 0)
            throw new BadRequestException("PageIndex must be greater than or equal to 0.");

        if (pageSize is < 1 or > MaxPageSize)
            throw new BadRequestException($"PageSize must be between 1 and {MaxPageSize}.");

        // Un paciente inactivo también tiene historia
        var patientId = new PatientId(query.PatientId);
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, cancellationToken))
            throw new NotFoundException(nameof(Patient), query.PatientId);

        var source = db.Consultations.AsNoTracking().Where(c => c.PatientId == patientId);

        var count = await source.LongCountAsync(cancellationToken);

        var consultations = await source
            .OrderByDescending(c => c.StartedAtUtc)
            .ThenByDescending(c => c.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToDto(db.Patients.AsNoTracking(), db.Doctors.AsNoTracking())
            .ToListAsync(cancellationToken);

        return new PaginatedResult<ConsultationDto>(
            pageIndex,
            pageSize,
            count,
            consultations.Select(c => c.WithLocalTimes(clinicTimeZone)).ToList());
    }
}
