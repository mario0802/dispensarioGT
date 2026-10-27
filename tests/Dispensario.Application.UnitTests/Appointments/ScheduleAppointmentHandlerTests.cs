using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Appointments.Commands.ScheduleAppointment;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Appointments;

// El reloj falso marca 2026-10-26 12:00 UTC; el 3 de noviembre de 2026 es martes (Guayaquil, UTC-5)
public sealed class ScheduleAppointmentHandlerTests : IDisposable
{
    private static readonly DateTime TuesdayAt0930 = new(2026, 11, 3, 9, 30, 0);

    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithinScheduleBlock_SchedulesAppointmentInUtc()
    {
        var (patient, doctor) = await SeedAsync();

        var result = await HandleAsync(NewCommand(patient, doctor));

        var appointment = await LoadAsync(result.Id);
        appointment.PatientId.ShouldBe(patient.Id);
        appointment.DoctorId.ShouldBe(doctor.Id);
        appointment.StartUtc.ShouldBe(new DateTime(2026, 11, 3, 14, 30, 0, DateTimeKind.Utc));
        appointment.EndUtc.ShouldBe(new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc));
        appointment.Reason.ShouldBe("Control");
        appointment.Status.ShouldBe(AppointmentStatus.Scheduled);
    }

    [Fact]
    public async Task Handle_WithUnknownPatient_ThrowsNotFoundException()
    {
        var (_, doctor) = await SeedAsync();

        var command = new ScheduleAppointmentCommand(Guid.CreateVersion7(), doctor.Id.Value, TuesdayAt0930, "Control");

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(command));
    }

    [Fact]
    public async Task Handle_WithUnknownDoctor_ThrowsNotFoundException()
    {
        var (patient, _) = await SeedAsync();

        var command = new ScheduleAppointmentCommand(patient.Id.Value, Guid.CreateVersion7(), TuesdayAt0930, "Control");

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(command));
    }

    [Fact]
    public async Task Handle_WithInactivePatient_ThrowsBadRequestException()
    {
        var (patient, doctor) = await SeedAsync(patientIsActive: false);

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(NewCommand(patient, doctor)));

        exception.Message.ShouldBe("Patient is inactive.");
    }

    [Fact]
    public async Task Handle_WithInactiveDoctor_ThrowsBadRequestException()
    {
        var (patient, doctor) = await SeedAsync(doctorIsActive: false);

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(NewCommand(patient, doctor)));

        exception.Message.ShouldBe("Doctor is inactive.");
    }

    [Theory]
    [InlineData(13, 0)]    // fuera de cualquier bloque
    [InlineData(11, 45)]   // empieza dentro del bloque y termina a las 12:15
    public async Task Handle_OutsideSchedule_ThrowsBadRequestException(int hour, int minute)
    {
        var (patient, doctor) = await SeedAsync();

        var exception = await Should.ThrowAsync<BadRequestException>(
            () => HandleAsync(NewCommand(patient, doctor, new DateTime(2026, 11, 3, hour, minute, 0))));

        exception.Message.ShouldBe("The doctor is not available at that time.");
    }

    [Fact]
    public async Task Handle_WithMisalignedStart_ThrowsBadRequestException()
    {
        var (patient, doctor) = await SeedAsync();

        var exception = await Should.ThrowAsync<BadRequestException>(
            () => HandleAsync(NewCommand(patient, doctor, new DateTime(2026, 11, 3, 9, 10, 0))));

        exception.Message.ShouldBe("Start time must align with the doctor's appointment slots.");
    }

    [Fact]
    public async Task Handle_WithBlockStartingAtTenPast_AlignsFromBlockStart()
    {
        var (patient, doctor) = await SeedAsync(blockStart: new TimeOnly(9, 10), blockEnd: new TimeOnly(12, 10));

        var result = await HandleAsync(NewCommand(patient, doctor, new DateTime(2026, 11, 3, 9, 40, 0)));

        (await LoadAsync(result.Id)).StartUtc.ShouldBe(new DateTime(2026, 11, 3, 14, 40, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_InThePast_ThrowsDomainException()
    {
        var (patient, doctor) = await SeedAsync();

        // 20 de octubre de 2026: martes anterior al reloj falso
        var exception = await Should.ThrowAsync<DomainException>(
            () => HandleAsync(NewCommand(patient, doctor, new DateTime(2026, 10, 20, 9, 30, 0))));

        exception.Message.ShouldBe("Appointment cannot be scheduled in the past.");
    }

    [Fact]
    public async Task Handle_OverlappingDoctorAppointment_ThrowsConflictException()
    {
        var (patient, doctor) = await SeedAsync();
        await AddAsync(TestAppointments.Create(
            doctorId: doctor.Id, startUtc: new DateTime(2026, 11, 3, 14, 45, 0, DateTimeKind.Utc)));

        var exception = await Should.ThrowAsync<ConflictException>(() => HandleAsync(NewCommand(patient, doctor)));

        exception.Message.ShouldBe("The doctor already has an appointment at that time.");
    }

    [Fact]
    public async Task Handle_OverlappingPatientAppointmentWithOtherDoctor_ThrowsConflictException()
    {
        var (patient, doctor) = await SeedAsync();
        await AddAsync(TestAppointments.Create(
            patientId: patient.Id, startUtc: new DateTime(2026, 11, 3, 14, 30, 0, DateTimeKind.Utc)));

        var exception = await Should.ThrowAsync<ConflictException>(() => HandleAsync(NewCommand(patient, doctor)));

        exception.Message.ShouldBe("The patient already has an appointment at that time.");
    }

    [Fact]
    public async Task Handle_WithContiguousAppointment_SchedulesAppointment()
    {
        var (patient, doctor) = await SeedAsync();
        await AddAsync(TestAppointments.Create(
            doctorId: doctor.Id, startUtc: new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc)));

        var result = await HandleAsync(NewCommand(patient, doctor));

        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_WithCancelledAppointmentAtSameTime_SchedulesAppointment()
    {
        var (patient, doctor) = await SeedAsync();
        await AddAsync(TestAppointments.Create(
            doctorId: doctor.Id, patientId: patient.Id, startUtc: new DateTime(2026, 11, 3, 14, 30, 0, DateTimeKind.Utc)));

        // Hasta la fila 13 no hay Cancel en el dominio: el estado se cambia directamente en la tabla
        await using (var command = _db.Connection.CreateCommand())
        {
            command.CommandText = "UPDATE Appointments SET Status = 'Cancelled'";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var result = await HandleAsync(NewCommand(patient, doctor));

        result.Id.ShouldNotBe(Guid.Empty);
    }

    public void Dispose() => _db.Dispose();

    private static ScheduleAppointmentCommand NewCommand(Patient patient, Doctor doctor, DateTime? startLocal = null) =>
        new(patient.Id.Value, doctor.Id.Value, startLocal ?? TuesdayAt0930, "Control");

    // Paciente y doctor con un bloque de martes (09:00–12:00 por defecto) y citas de 30 minutos
    private async Task<(Patient Patient, Doctor Doctor)> SeedAsync(
        bool patientIsActive = true,
        bool doctorIsActive = true,
        TimeOnly? blockStart = null,
        TimeOnly? blockEnd = null)
    {
        var patient = TestPatients.Create();
        if (!patientIsActive)
            patient.Deactivate();

        var doctor = TestDoctors.Create();
        doctor.SetWeeklySchedule(
            [ScheduleBlock.Create(DayOfWeek.Tuesday, blockStart ?? new TimeOnly(9, 0), blockEnd ?? new TimeOnly(12, 0))]);
        if (!doctorIsActive)
            doctor.Deactivate();

        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        context.Doctors.Add(doctor);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (patient, doctor);
    }

    private async Task AddAsync(Appointment appointment)
    {
        await using var context = _db.CreateContext();
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Appointment> LoadAsync(Guid id)
    {
        var appointmentId = new AppointmentId(id);
        await using var context = _db.CreateContext();
        return await context.Appointments.SingleAsync(a => a.Id == appointmentId, TestContext.Current.CancellationToken);
    }

    private async Task<ScheduleAppointmentResult> HandleAsync(ScheduleAppointmentCommand command)
    {
        await using var context = _db.CreateContext();
        return await new ScheduleAppointmentHandler(context, _db.ClinicTimeZone, _db.TimeProvider)
            .Handle(command, TestContext.Current.CancellationToken);
    }
}
