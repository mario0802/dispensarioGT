using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;

namespace Dispensario.Application.UnitTests.Common;

// Doctores de prueba válidos
public static class TestDoctors
{
    public static Doctor Create(
        string licenseNumber = "MP-123",
        string firstName = "Luis",
        string lastName = "Mora",
        string specialty = "Pediatría",
        string email = "luis.mora@dispensario.local",
        string? phone = null,
        int appointmentDurationMinutes = Doctor.DefaultAppointmentDurationMinutes) => Doctor.Create(
        FullName.Create(firstName, lastName),
        licenseNumber,
        specialty,
        Email.Create(email),
        phone is null ? null : PhoneNumber.Create(phone),
        appointmentDurationMinutes);
}
