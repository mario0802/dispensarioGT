using BuildingBlocks.CQRS;
using Dispensario.Application.Doctors.Dtos;

namespace Dispensario.Application.Doctors.Queries.GetDoctorSchedule;

public sealed record GetDoctorScheduleQuery(Guid DoctorId) : IQuery<DoctorScheduleDto>;
