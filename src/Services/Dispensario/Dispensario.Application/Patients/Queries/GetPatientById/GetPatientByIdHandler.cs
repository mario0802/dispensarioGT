using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Patients.Dtos;
using Dispensario.Domain.Patients;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Patients.Queries.GetPatientById;

/// <summary>
/// Devuelve un paciente por su ID.
/// </summary>
public sealed class GetPatientByIdHandler(IApplicationDbContext db) : IQueryHandler<GetPatientByIdQuery, PatientDto>
{
    public async Task<PatientDto> Handle(GetPatientByIdQuery query, CancellationToken cancellationToken)
    {
        var patientId = new PatientId(query.PatientId);

        return await db.Patients.AsNoTracking()
                .Where(p => p.Id == patientId)
                .ProjectToType<PatientDto>()
                .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Patient), query.PatientId);
    }
}
