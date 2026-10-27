using BuildingBlocks.CQRS;
using Dispensario.Application.Appointments.Dtos;

namespace Dispensario.Application.Appointments.Queries.GetAppointmentById;

public sealed record GetAppointmentByIdQuery(Guid AppointmentId) : IQuery<AppointmentDto>;
