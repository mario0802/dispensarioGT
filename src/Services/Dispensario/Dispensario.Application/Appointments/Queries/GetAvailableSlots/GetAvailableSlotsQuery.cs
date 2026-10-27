using BuildingBlocks.CQRS;
using Dispensario.Application.Appointments.Dtos;

namespace Dispensario.Application.Appointments.Queries.GetAvailableSlots;

// Date es una fecha local del dispensario
public sealed record GetAvailableSlotsQuery(Guid DoctorId, DateOnly Date) : IQuery<AvailableSlotsDto>;
