using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Doctors.Dtos;
using Dispensario.Domain.Doctors;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Doctors.Queries.GetDoctorById;

/// <summary>
/// Devuelve un doctor por su ID, con el usuario vinculado si lo tiene.
/// </summary>
public sealed class GetDoctorByIdHandler(IApplicationDbContext db) : IQueryHandler<GetDoctorByIdQuery, DoctorDto>
{
    public async Task<DoctorDto> Handle(GetDoctorByIdQuery query, CancellationToken cancellationToken)
    {
        var doctorId = new DoctorId(query.DoctorId);

        return await db.Doctors.AsNoTracking()
                .Where(d => d.Id == doctorId)
                .ToDto(db.Users.AsNoTracking())
                .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Doctor), query.DoctorId);
    }
}
