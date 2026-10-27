using BuildingBlocks.CQRS;
using Dispensario.Application.Consultations.Dtos;

namespace Dispensario.Application.Consultations.Queries.GetConsultationById;

public sealed record GetConsultationByIdQuery(Guid ConsultationId) : IQuery<ConsultationDto>;
