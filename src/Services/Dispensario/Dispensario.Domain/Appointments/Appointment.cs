using BuildingBlocks.Domain;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;

namespace Dispensario.Domain.Appointments;

// Cita de un paciente con un doctor. Las reglas que dependen de otros aggregates
// (horario del doctor, cruces, paciente y doctor activos) las valida el handler
public sealed class Appointment : AggregateRoot<AppointmentId>
{
    public const int ReasonMaxLength = 500;

    // Constructor para EF
    private Appointment()
    {
        Reason = null!;
    }

    private Appointment(AppointmentId id)
        : this()
    {
        Id = id;
    }

    public PatientId PatientId { get; private set; }
    public DoctorId DoctorId { get; private set; }
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public string Reason { get; private set; }
    public AppointmentStatus Status { get; private set; }

    // nowUtc lo pasa el handler (TimeProvider): el dominio no conoce la hora actual
    public static Appointment Create(
        PatientId patientId,
        DoctorId doctorId,
        DateTime startUtc,
        DateTime endUtc,
        string reason,
        DateTime nowUtc)
    {
        if (startUtc >= endUtc)
            throw new DomainException("Appointment start must be before its end.");

        if (startUtc <= nowUtc)
            throw new DomainException("Appointment cannot be scheduled in the past.");

        var normalizedReason = NormalizeReason(reason);

        return new Appointment(AppointmentId.New())
        {
            PatientId = patientId,
            DoctorId = doctorId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Reason = normalizedReason,
            Status = AppointmentStatus.Scheduled
        };
    }

    private static string NormalizeReason(string reason)
    {
        var normalized = reason?.Trim() ?? string.Empty;

        if (normalized.Length == 0)
            throw new DomainException("Reason is required.");

        if (normalized.Length > ReasonMaxLength)
            throw new DomainException($"Reason must be at most {ReasonMaxLength} characters.");

        return normalized;
    }
}
