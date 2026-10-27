using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Appointments.Commands.RescheduleAppointment;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Appointments;

// El reloj falso marca 2026-10-26 12:00 UTC; la cita de partida es el martes 3 de noviembre a las 09:30 locales
// (14:30 UTC, Guayaquil UTC-5) y el doctor atiende los martes de 09:00 a 12:00
public sealed class RescheduleAppointmentHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithinSchedule_MovesAppointmentWithCurrentDurationAndResetsToScheduled()
    {
        // La cita original dura 60 minutos; el doctor atiende hoy en 30
        var (_, _, appointment) = await SeedAsync(appointmentDurationMinutes: 60, confirm: true);

        await HandleAsync(appointment.Id.Value, Tuesday(10, 30));

        var loaded = await LoadAsync(appointment.Id);
        loaded.StartUtc.ShouldBe(new DateTime(2026, 11, 3, 15, 30, 0, DateTimeKind.Utc));
        loaded.EndUtc.ShouldBe(new DateTime(2026, 11, 3, 16, 0, 0, DateTimeKind.Utc));
        loaded.Status.ShouldBe(AppointmentStatus.Scheduled);
    }

    [Fact]
    public async Task Handle_OverlappingOnlyItself_MovesAppointment()
    {
        // Citas de 15 minutos: 09:45–10:00 se solapa con la propia cita original (09:30–10:00)
        var (_, _, appointment) = await SeedAsync(doctorDurationMinutes: 15);

        await HandleAsync(appointment.Id.Value, Tuesday(9, 45));

        (await LoadAsync(appointment.Id)).StartUtc.ShouldBe(new DateTime(2026, 11, 3, 14, 45, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_WithUnknownId_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Guid.CreateVersion7(), Tuesday(10, 30)));
    }

    [Fact]
    public async Task Handle_WithInactivePatient_ThrowsBadRequestException()
    {
        var (_, _, appointment) = await SeedAsync(patientIsActive: false);

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(appointment.Id.Value, Tuesday(10, 30)));

        exception.Message.ShouldBe("Patient is inactive.");
    }

    [Fact]
    public async Task Handle_WithInactiveDoctor_ThrowsBadRequestException()
    {
        var (_, _, appointment) = await SeedAsync(doctorIsActive: false);

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(appointment.Id.Value, Tuesday(10, 30)));

        exception.Message.ShouldBe("Doctor is inactive.");
    }

    [Fact]
    public async Task Handle_OutsideSchedule_ThrowsBadRequestException()
    {
        var (_, _, appointment) = await SeedAsync();

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(appointment.Id.Value, Tuesday(13, 0)));

        exception.Message.ShouldBe("The doctor is not available at that time.");
        (await LoadAsync(appointment.Id)).StartUtc.ShouldBe(TestAppointments.DefaultStartUtc);
    }

    [Fact]
    public async Task Handle_WithMisalignedStart_ThrowsBadRequestException()
    {
        var (_, _, appointment) = await SeedAsync();

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(appointment.Id.Value, Tuesday(10, 10)));

        exception.Message.ShouldBe("Start time must align with the doctor's appointment slots.");
    }

    [Fact]
    public async Task Handle_OverlappingOtherDoctorAppointment_ThrowsConflictException()
    {
        var (_, doctor, appointment) = await SeedAsync();
        await AddAsync(TestAppointments.Create(
            doctorId: doctor.Id, startUtc: new DateTime(2026, 11, 3, 15, 30, 0, DateTimeKind.Utc)));

        var exception = await Should.ThrowAsync<ConflictException>(() => HandleAsync(appointment.Id.Value, Tuesday(10, 30)));

        exception.Message.ShouldBe("The doctor already has an appointment at that time.");
        (await LoadAsync(appointment.Id)).StartUtc.ShouldBe(TestAppointments.DefaultStartUtc);
    }

    [Fact]
    public async Task Handle_OverlappingOtherPatientAppointment_ThrowsConflictException()
    {
        var (patient, _, appointment) = await SeedAsync();
        await AddAsync(TestAppointments.Create(
            patientId: patient.Id, startUtc: new DateTime(2026, 11, 3, 15, 30, 0, DateTimeKind.Utc)));

        var exception = await Should.ThrowAsync<ConflictException>(() => HandleAsync(appointment.Id.Value, Tuesday(10, 30)));

        exception.Message.ShouldBe("The patient already has an appointment at that time.");
    }

    [Fact]
    public async Task Handle_WithSameStart_ThrowsDomainException()
    {
        var (_, _, appointment) = await SeedAsync();

        var exception = await Should.ThrowAsync<DomainException>(() => HandleAsync(appointment.Id.Value, Tuesday(9, 30)));

        exception.Message.ShouldBe("New start must be different from the current one.");
    }

    [Fact]
    public async Task Handle_ToThePast_ThrowsDomainException()
    {
        var (_, _, appointment) = await SeedAsync();

        // 20 de octubre de 2026: martes anterior al reloj falso
        var exception = await Should.ThrowAsync<DomainException>(
            () => HandleAsync(appointment.Id.Value, new DateTime(2026, 10, 20, 9, 30, 0)));

        exception.Message.ShouldBe("Appointment cannot be scheduled in the past.");
    }

    public void Dispose() => _db.Dispose();

    private static DateTime Tuesday(int hour, int minute) => new(2026, 11, 3, hour, minute, 0);

    // Paciente, doctor con bloque de martes 09:00–12:00 y una cita a las 09:30 locales
    private async Task<(Patient Patient, Doctor Doctor, Appointment Appointment)> SeedAsync(
        bool patientIsActive = true,
        bool doctorIsActive = true,
        int doctorDurationMinutes = Doctor.DefaultAppointmentDurationMinutes,
        int appointmentDurationMinutes = Doctor.DefaultAppointmentDurationMinutes,
        bool confirm = false)
    {
        var patient = TestPatients.Create();
        if (!patientIsActive)
            patient.Deactivate();

        var doctor = TestDoctors.Create(appointmentDurationMinutes: doctorDurationMinutes);
        doctor.SetWeeklySchedule([ScheduleBlock.Create(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(12, 0))]);
        if (!doctorIsActive)
            doctor.Deactivate();

        var appointment = TestAppointments.Create(patient.Id, doctor.Id, durationMinutes: appointmentDurationMinutes);
        if (confirm)
            appointment.Confirm(TestAppointments.NowUtc);

        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        context.Doctors.Add(doctor);
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (patient, doctor, appointment);
    }

    private async Task AddAsync(Appointment appointment)
    {
        await using var context = _db.CreateContext();
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Appointment> LoadAsync(AppointmentId id)
    {
        await using var context = _db.CreateContext();
        return await context.Appointments.SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task HandleAsync(Guid id, DateTime startLocal)
    {
        await using var context = _db.CreateContext();
        await new RescheduleAppointmentHandler(context, _db.ClinicTimeZone, _db.TimeProvider)
            .Handle(new RescheduleAppointmentCommand(id, startLocal), TestContext.Current.CancellationToken);
    }
}
