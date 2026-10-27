using BuildingBlocks.Domain;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;

namespace Dispensario.Domain.Consultations;

// Atención clínica de un paciente por un doctor, con o sin cita. Las reglas que dependen de otros
// aggregates (doctor y paciente activos, cita del doctor y de hoy, una consulta por cita) las valida el handler
public sealed class Consultation : AggregateRoot<ConsultationId>
{
    public const int ReasonMaxLength = 500;
    public const int DiagnosisMaxLength = 1000;
    public const int NotesMaxLength = 4000;

    // Constructor para EF
    private Consultation()
    {
        Reason = null!;
    }

    private Consultation(ConsultationId id)
        : this()
    {
        Id = id;
    }

    public PatientId PatientId { get; private set; }
    public DoctorId DoctorId { get; private set; }

    // null en una consulta sin cita
    public AppointmentId? AppointmentId { get; private set; }

    public string Reason { get; private set; }
    public string? Diagnosis { get; private set; }
    public string? Notes { get; private set; }
    public ConsultationStatus Status { get; private set; }
    public DateTime StartedAtUtc { get; private set; }

    // Solo tiene valor en las consultas cerradas
    public DateTime? ClosedAtUtc { get; private set; }

    // startedAtUtc lo pasa el handler (TimeProvider): el dominio no conoce la hora actual
    public static Consultation Start(
        PatientId patientId,
        DoctorId doctorId,
        AppointmentId? appointmentId,
        string reason,
        DateTime startedAtUtc)
    {
        var normalizedReason = NormalizeReason(reason);

        return new Consultation(ConsultationId.New())
        {
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentId = appointmentId,
            Reason = normalizedReason,
            Status = ConsultationStatus.Open,
            StartedAtUtc = startedAtUtc
        };
    }

    // Reemplaza los tres textos: un opcional null o en blanco borra el valor
    public void Update(string reason, string? diagnosis, string? notes)
    {
        if (Status != ConsultationStatus.Open)
            throw new DomainException("Only open consultations can be updated.");

        var normalizedReason = NormalizeReason(reason);
        var normalizedDiagnosis = NormalizeDiagnosis(diagnosis);
        var normalizedNotes = NormalizeNotes(notes);

        Reason = normalizedReason;
        Diagnosis = normalizedDiagnosis;
        Notes = normalizedNotes;
    }

    // Cerrar exige diagnóstico; Closed es final
    public void Close(string diagnosis, string? notes, DateTime closedAtUtc)
    {
        if (Status == ConsultationStatus.Closed)
            throw new DomainException("Consultation is already closed.");

        var normalizedDiagnosis = NormalizeDiagnosis(diagnosis)
            ?? throw new DomainException("Diagnosis is required to close a consultation.");
        var normalizedNotes = NormalizeNotes(notes);

        if (closedAtUtc < StartedAtUtc)
            throw new DomainException("Consultation cannot be closed before it started.");

        Diagnosis = normalizedDiagnosis;
        Notes = normalizedNotes;
        ClosedAtUtc = closedAtUtc;
        Status = ConsultationStatus.Closed;
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

    private static string? NormalizeDiagnosis(string? diagnosis)
    {
        var normalized = NormalizeOptional(diagnosis);

        if (normalized?.Length > DiagnosisMaxLength)
            throw new DomainException($"Diagnosis must be at most {DiagnosisMaxLength} characters.");

        return normalized;
    }

    private static string? NormalizeNotes(string? notes)
    {
        var normalized = NormalizeOptional(notes);

        if (normalized?.Length > NotesMaxLength)
            throw new DomainException($"Notes must be at most {NotesMaxLength} characters.");

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
