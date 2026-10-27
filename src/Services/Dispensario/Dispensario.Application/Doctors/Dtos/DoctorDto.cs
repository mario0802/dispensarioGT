namespace Dispensario.Application.Doctors.Dtos;

// UserId: usuario vinculado a la ficha (null si el doctor no tiene cuenta)
public sealed record DoctorDto(
    Guid Id,
    string FirstName,
    string LastName,
    string LicenseNumber,
    string Specialty,
    string Email,
    string? Phone,
    int AppointmentDurationMinutes,
    bool IsActive,
    Guid? UserId,
    DateTime CreatedAtUtc);
