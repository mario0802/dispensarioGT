using BuildingBlocks.Domain;
using Dispensario.Domain.Common;

namespace Dispensario.Domain.Patients;

// Ficha del paciente: documento único, datos personales y datos médicos básicos
public sealed class Patient : AggregateRoot<PatientId>
{
    public const int AllergiesMaxLength = 1000;

    // Constructor para EF: los VOs los materializa EF justo después
    private Patient()
    {
        Document = null!;
        FullName = null!;
    }

    private Patient(PatientId id)
        : this()
    {
        Id = id;
        IsActive = true;
    }

    public Document Document { get; private set; }
    public FullName FullName { get; private set; }
    public DateOnly BirthDate { get; private set; }
    public Gender Gender { get; private set; }
    public Email? Email { get; private set; }
    public PhoneNumber? Phone { get; private set; }
    public Address? Address { get; private set; }
    public BloodType? BloodType { get; private set; }
    public string? Allergies { get; private set; }
    public bool IsActive { get; private set; }

    // today lo pasa el handler (TimeProvider): el dominio no conoce la hora actual
    public static Patient Create(
        Document document,
        FullName fullName,
        DateOnly birthDate,
        Gender gender,
        Email? email,
        PhoneNumber? phone,
        Address? address,
        BloodType? bloodType,
        string? allergies,
        DateOnly today)
    {
        var patient = new Patient(PatientId.New());
        patient.Update(document, fullName, birthDate, gender, email, phone, address, bloodType, allergies, today);
        return patient;
    }

    // Reemplaza todos los datos; un opcional null borra el valor actual. También vale sobre un paciente inactivo
    public void Update(
        Document document,
        FullName fullName,
        DateOnly birthDate,
        Gender gender,
        Email? email,
        PhoneNumber? phone,
        Address? address,
        BloodType? bloodType,
        string? allergies,
        DateOnly today)
    {
        if (birthDate > today)
            throw new DomainException("Birth date cannot be in the future.");

        if (!Enum.IsDefined(gender))
            throw new DomainException("Gender is invalid.");

        if (bloodType is { } value && !Enum.IsDefined(value))
            throw new DomainException("Blood type is invalid.");

        var normalizedAllergies = NormalizeAllergies(allergies);

        Document = document;
        FullName = fullName;
        BirthDate = birthDate;
        Gender = gender;
        Email = email;
        Phone = phone;
        Address = address;
        BloodType = bloodType;
        Allergies = normalizedAllergies;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("Patient is already inactive.");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("Patient is already active.");

        IsActive = true;
    }

    private static string? NormalizeAllergies(string? allergies)
    {
        if (string.IsNullOrWhiteSpace(allergies))
            return null;

        var normalized = allergies.Trim();

        if (normalized.Length > AllergiesMaxLength)
            throw new DomainException($"Allergies must be at most {AllergiesMaxLength} characters.");

        return normalized;
    }
}
