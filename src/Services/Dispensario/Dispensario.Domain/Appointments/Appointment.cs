using BuildingBlocks.Domain;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;

namespace Dispensario.Domain.Appointments;

// Cita de un paciente con un doctor. Las reglas que dependen de otros aggregates
// (horario del doctor, cruces, paciente y doctor activos) las valida el handler
public sealed class Appointment : AggregateRoot<AppointmentId>
{
    public const int ReasonMaxLength = 500;
    public const int CancellationReasonMaxLength = 500;

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

    // Solo tiene valor en las citas canceladas
    public string? CancellationReason { get; private set; }

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

    // Las transiciones reciben nowUtc del handler, igual que Create
    public void Confirm(DateTime nowUtc)
    {
        if (Status != AppointmentStatus.Scheduled)
            throw new DomainException("Only scheduled appointments can be confirmed.");

        EnsureNotStarted(nowUtc);

        Status = AppointmentStatus.Confirmed;
    }

    public void Cancel(string reason, DateTime nowUtc)
    {
        if (!IsPending)
            throw new DomainException("Only scheduled or confirmed appointments can be cancelled.");

        EnsureNotStarted(nowUtc);

        var normalizedReason = reason?.Trim() ?? string.Empty;

        if (normalizedReason.Length == 0)
            throw new DomainException("Cancellation reason is required.");

        if (normalizedReason.Length > CancellationReasonMaxLength)
            throw new DomainException($"Cancellation reason must be at most {CancellationReasonMaxLength} characters.");

        CancellationReason = normalizedReason;
        Status = AppointmentStatus.Cancelled;
    }

    // Solo desde la hora de inicio: antes aún puede llegar el paciente
    public void MarkNoShow(DateTime nowUtc)
    {
        if (!IsPending)
            throw new DomainException("Only scheduled or confirmed appointments can be marked as no-show.");

        if (StartUtc > nowUtc)
            throw new DomainException("Appointment has not started yet.");

        Status = AppointmentStatus.NoShow;
    }

    // La nueva hora necesita una nueva confirmación: la cita vuelve a Scheduled
    public void Reschedule(DateTime startUtc, DateTime endUtc, DateTime nowUtc)
    {
        if (!IsPending)
            throw new DomainException("Only scheduled or confirmed appointments can be rescheduled.");

        EnsureNotStarted(nowUtc);

        if (startUtc == StartUtc)
            throw new DomainException("New start must be different from the current one.");

        if (startUtc >= endUtc)
            throw new DomainException("Appointment start must be before its end.");

        if (startUtc <= nowUtc)
            throw new DomainException("Appointment cannot be scheduled in the past.");

        StartUtc = startUtc;
        EndUtc = endUtc;
        Status = AppointmentStatus.Scheduled;
    }

    private bool IsPending => Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed;

    private void EnsureNotStarted(DateTime nowUtc)
    {
        if (StartUtc <= nowUtc)
            throw new DomainException("Appointment has already started.");
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
