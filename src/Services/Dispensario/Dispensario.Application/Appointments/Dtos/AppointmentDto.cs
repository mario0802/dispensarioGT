using Dispensario.Domain.Appointments;

namespace Dispensario.Application.Appointments.Dtos;

// StartLocal y EndLocal en hora local del dispensario (sin offset); StartUtc y EndUtc en UTC
public sealed record AppointmentDto(
    Guid Id,
    Guid PatientId,
    string PatientFullName,
    Guid DoctorId,
    string DoctorFullName,
    DateTime StartUtc,
    DateTime EndUtc,
    DateTime StartLocal,
    DateTime EndLocal,
    string Reason,
    AppointmentStatus Status,
    DateTime CreatedAtUtc);
