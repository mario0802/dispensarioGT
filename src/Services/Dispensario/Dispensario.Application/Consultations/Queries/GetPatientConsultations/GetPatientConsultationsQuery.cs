using BuildingBlocks.CQRS;
using BuildingBlocks.Pagination;
using Dispensario.Application.Consultations.Dtos;

namespace Dispensario.Application.Consultations.Queries.GetPatientConsultations;

// Historia clínica del paciente: todas sus consultas, de cualquier doctor, de la más reciente a la más antigua
public sealed record GetPatientConsultationsQuery(Guid PatientId, PaginationRequest Pagination)
    : IQuery<PaginatedResult<ConsultationDto>>;
