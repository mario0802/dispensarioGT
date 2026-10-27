using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Consultations.Dtos;
using Dispensario.Domain.Consultations;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Consultations.Queries.GetConsultationById;

/// <summary>
/// Devuelve una consulta por su ID; la leen el Admin y cualquier doctor vinculado.
/// </summary>
public sealed class GetConsultationByIdHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IClinicTimeZone clinicTimeZone)
    : IQueryHandler<GetConsultationByIdQuery, ConsultationDto>
{
    public async Task<ConsultationDto> Handle(GetConsultationByIdQuery query, CancellationToken cancellationToken)
    {
        // Antes de leer la BD; el Admin no necesita vínculo
        if (currentUser.Role == Role.Doctor && currentUser.DoctorId is null)
            throw new ForbiddenException("Your user is not linked to a doctor.");

        var consultationId = new ConsultationId(query.ConsultationId);
        var consultation = await db.Consultations
            .AsNoTracking()
            .Where(c => c.Id == consultationId)
            .ToDto(db.Patients.AsNoTracking(), db.Doctors.AsNoTracking())
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Consultation), query.ConsultationId);

        return consultation.WithLocalTimes(clinicTimeZone);
    }
}
