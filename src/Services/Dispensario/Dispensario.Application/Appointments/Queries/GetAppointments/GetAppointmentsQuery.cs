using BuildingBlocks.CQRS;
using BuildingBlocks.Pagination;
using Dispensario.Application.Appointments.Dtos;
using Dispensario.Domain.Appointments;

namespace Dispensario.Application.Appointments.Queries.GetAppointments;

// From y To son fechas locales del dispensario, ambas inclusive
public sealed record GetAppointmentsQuery(
    PaginationRequest Pagination,
    Guid? DoctorId,
    Guid? PatientId,
    AppointmentStatus? Status,
    DateOnly? From,
    DateOnly? To) : IQuery<PaginatedResult<AppointmentDto>>;
