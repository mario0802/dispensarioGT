using BuildingBlocks.CQRS;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.Patients.Dtos;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.Patients.Queries.GetPatientByDocument;

/// <summary>
/// Busca un paciente por su documento exacto (tipo + número), también si está inactivo.
/// </summary>
public sealed class GetPatientByDocumentHandler(IApplicationDbContext db) : IQueryHandler<GetPatientByDocumentQuery, PatientDto>
{
    public async Task<PatientDto> Handle(GetPatientByDocumentQuery query, CancellationToken cancellationToken)
    {
        // Las queries no pasan por ValidationBehavior: el número se valida aquí
        var number = query.DocumentNumber?.Trim();
        if (string.IsNullOrEmpty(number))
            throw new BadRequestException("Document number is required.");

        // Igualdad exacta, como el índice único: el número distingue mayúsculas
        return await db.Patients.AsNoTracking()
                .Where(p => p.Document.Type == query.DocumentType && p.Document.Number == number)
                .ProjectToType<PatientDto>()
                .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Patient with document {query.DocumentType} {number} was not found.");
    }
}
