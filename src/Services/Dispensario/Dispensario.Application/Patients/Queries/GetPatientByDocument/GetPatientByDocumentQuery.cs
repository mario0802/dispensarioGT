using BuildingBlocks.CQRS;
using Dispensario.Application.Patients.Dtos;
using Dispensario.Domain.Common;

namespace Dispensario.Application.Patients.Queries.GetPatientByDocument;

public sealed record GetPatientByDocumentQuery(DocumentType DocumentType, string DocumentNumber) : IQuery<PatientDto>;
