using Dispensario.Domain.Consultations;

namespace Dispensario.Application.Consultations.Dtos;

// StartedAtLocal y ClosedAtLocal en hora local del dispensario (sin offset); StartedAtUtc y ClosedAtUtc en UTC
public sealed record ConsultationDto(
    Guid Id,
    Guid PatientId,
    string PatientFullName,
    Guid DoctorId,
    string DoctorFullName,
    Guid? AppointmentId,
    string Reason,
    string? Diagnosis,
    string? Notes,
    ConsultationStatus Status,
    DateTime StartedAtUtc,
    DateTime? ClosedAtUtc,
    DateTime StartedAtLocal,
    DateTime? ClosedAtLocal,
    DateTime CreatedAtUtc);
