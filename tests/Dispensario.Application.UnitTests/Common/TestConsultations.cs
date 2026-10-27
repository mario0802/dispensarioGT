using Dispensario.Domain.Appointments;
using Dispensario.Domain.Consultations;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;

namespace Dispensario.Application.UnitTests.Common;

// Consultas de prueba válidas, iniciadas en la hora del reloj falso de DispensarioTestDb
public static class TestConsultations
{
    public static readonly DateTime StartedAtUtc = Persistence.Fakes.DispensarioTestDb.StartTime.UtcDateTime;

    public static Consultation Start(
        PatientId? patientId = null,
        DoctorId? doctorId = null,
        AppointmentId? appointmentId = null,
        string reason = "Dolor de cabeza",
        DateTime? startedAtUtc = null) => Consultation.Start(
        patientId ?? PatientId.New(),
        doctorId ?? DoctorId.New(),
        appointmentId,
        reason,
        startedAtUtc ?? StartedAtUtc);
}
