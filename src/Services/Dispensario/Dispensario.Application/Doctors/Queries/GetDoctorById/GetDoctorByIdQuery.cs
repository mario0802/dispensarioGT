using BuildingBlocks.CQRS;
using Dispensario.Application.Doctors.Dtos;

namespace Dispensario.Application.Doctors.Queries.GetDoctorById;

public sealed record GetDoctorByIdQuery(Guid DoctorId) : IQuery<DoctorDto>;
