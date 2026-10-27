using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;

namespace Dispensario.Application.UnitTests.Common;

// Citas de prueba válidas (futuras respecto al reloj falso de DispensarioTestDb)
public static class TestAppointments
{
    public static readonly DateTime NowUtc = Persistence.Fakes.DispensarioTestDb.StartTime.UtcDateTime;
    public static readonly DateTime DefaultStartUtc = new(2026, 11, 3, 14, 30, 0, DateTimeKind.Utc);

    public static Appointment Create(
        PatientId? patientId = null,
        DoctorId? doctorId = null,
        DateTime? startUtc = null,
        int durationMinutes = Doctor.DefaultAppointmentDurationMinutes,
        string reason = "Control")
    {
        var start = startUtc ?? DefaultStartUtc;
        return Appointment.Create(
            patientId ?? PatientId.New(),
            doctorId ?? DoctorId.New(),
            start,
            start.AddMinutes(durationMinutes),
            reason,
            NowUtc);
    }
}
