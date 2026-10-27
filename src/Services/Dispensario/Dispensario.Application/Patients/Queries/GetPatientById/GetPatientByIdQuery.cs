using BuildingBlocks.CQRS;
using Dispensario.Application.Patients.Dtos;

namespace Dispensario.Application.Patients.Queries.GetPatientById;

public sealed record GetPatientByIdQuery(Guid PatientId) : IQuery<PatientDto>;
