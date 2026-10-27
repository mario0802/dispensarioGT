using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;

namespace Dispensario.Application.Patients.Dtos;

// BloodType en notación clínica ("A+"); Address null si el paciente no tiene dirección
public sealed record PatientDto(
    Guid Id,
    DocumentType DocumentType,
    string DocumentNumber,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    Gender Gender,
    string? Email,
    string? Phone,
    AddressDto? Address,
    string? BloodType,
    string? Allergies,
    bool IsActive,
    DateTime CreatedAtUtc);
