using BuildingBlocks.Domain;
using Dispensario.Domain.Common;

namespace Dispensario.Domain.Doctors;

// Ficha del doctor; el horario semanal llega con la fila 10
public sealed class Doctor : AggregateRoot<DoctorId>
{
    public const int LicenseNumberMaxLength = 30;
    public const int SpecialtyMaxLength = 100;
    public const int MinAppointmentDurationMinutes = 10;
    public const int MaxAppointmentDurationMinutes = 120;
    public const int AppointmentDurationStepMinutes = 5;
    public const int DefaultAppointmentDurationMinutes = 30;

    // Constructor para EF: los VOs y los textos los materializa EF justo después
    private Doctor()
    {
        FullName = null!;
        LicenseNumber = null!;
        Specialty = null!;
        Email = null!;
    }

    private Doctor(DoctorId id)
        : this()
    {
        Id = id;
        IsActive = true;
    }

    public FullName FullName { get; private set; }
    public string LicenseNumber { get; private set; }
    public string Specialty { get; private set; }
    public Email Email { get; private set; }
    public PhoneNumber? Phone { get; private set; }
    public int AppointmentDurationMinutes { get; private set; }
    public bool IsActive { get; private set; }

    public static Doctor Create(
        FullName fullName,
        string licenseNumber,
        string specialty,
        Email email,
        PhoneNumber? phone,
        int appointmentDurationMinutes)
    {
        var doctor = new Doctor(DoctorId.New());
        doctor.Update(fullName, licenseNumber, specialty, email, phone, appointmentDurationMinutes);
        return doctor;
    }

    // Reemplaza todos los datos; un teléfono null borra el que hubiera. También vale sobre un doctor inactivo
    public void Update(
        FullName fullName,
        string licenseNumber,
        string specialty,
        Email email,
        PhoneNumber? phone,
        int appointmentDurationMinutes)
    {
        var normalizedLicense = NormalizeLicenseNumber(licenseNumber);
        var normalizedSpecialty = NormalizeSpecialty(specialty);
        EnsureAppointmentDuration(appointmentDurationMinutes);

        FullName = fullName;
        LicenseNumber = normalizedLicense;
        Specialty = normalizedSpecialty;
        Email = email;
        Phone = phone;
        AppointmentDurationMinutes = appointmentDurationMinutes;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("Doctor is already inactive.");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("Doctor is already active.");

        IsActive = true;
    }

    // En mayúsculas para que la unicidad no distinga mayúsculas (SQLite compara el texto tal cual)
    private static string NormalizeLicenseNumber(string licenseNumber)
    {
        var normalized = licenseNumber?.Trim().ToUpperInvariant() ?? string.Empty;

        if (normalized.Length == 0)
            throw new DomainException("License number is required.");

        if (normalized.Length > LicenseNumberMaxLength)
            throw new DomainException($"License number must be at most {LicenseNumberMaxLength} characters.");

        return normalized;
    }

    private static string NormalizeSpecialty(string specialty)
    {
        var normalized = specialty?.Trim() ?? string.Empty;

        if (normalized.Length == 0)
            throw new DomainException("Specialty is required.");

        if (normalized.Length > SpecialtyMaxLength)
            throw new DomainException($"Specialty must be at most {SpecialtyMaxLength} characters.");

        return normalized;
    }

    private static void EnsureAppointmentDuration(int minutes)
    {
        if (minutes is < MinAppointmentDurationMinutes or > MaxAppointmentDurationMinutes
            || minutes % AppointmentDurationStepMinutes != 0)
            throw new DomainException(
                $"Appointment duration must be between {MinAppointmentDurationMinutes} and {MaxAppointmentDurationMinutes} minutes and a multiple of {AppointmentDurationStepMinutes}.");
    }
}
