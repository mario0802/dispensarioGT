using BuildingBlocks.Exceptions;
using Dispensario.Application.Consultations.Commands.StartConsultation;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Consultations;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Consultations;

// Reloj falso: 2026-10-26 12:00 UTC, que en America/Guayaquil (UTC-5) es el 26 a las 07:00
public sealed class StartConsultationHandlerTests : IDisposable
{
    private static readonly DateTime TodayLaterUtc = new(2026, 10, 26, 15, 0, 0, DateTimeKind.Utc);

    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithoutAppointment_PersistsOpenConsultationOfCurrentDoctor()
    {
        var (patient, doctor) = await SeedPatientAndDoctorAsync();
        ActAsDoctor(doctor.Id);

        var result = await HandleAsync(patient.Id.Value, reason: "  Dolor de cabeza  ");

        var loaded = await LoadAsync(new ConsultationId(result.Id));
        loaded.PatientId.ShouldBe(patient.Id);
        loaded.DoctorId.ShouldBe(doctor.Id);
        loaded.AppointmentId.ShouldBeNull();
        loaded.Reason.ShouldBe("Dolor de cabeza");
        loaded.Status.ShouldBe(ConsultationStatus.Open);
        loaded.StartedAtUtc.ShouldBe(DispensarioTestDb.StartTime.UtcDateTime);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_WithPendingAppointmentOfToday_PersistsConsultationAndKeepsAppointmentStatus(bool confirmed)
    {
        var (patient, doctor) = await SeedPatientAndDoctorAsync();
        var appointment = CreateAppointment(patient.Id, doctor.Id, TodayLaterUtc);
        if (confirmed)
            appointment.Confirm(TestAppointments.NowUtc);
        await AddAsync(appointment);
        ActAsDoctor(doctor.Id);

        var result = await HandleAsync(patient.Id.Value, appointment.Id.Value);

        (await LoadAsync(new ConsultationId(result.Id))).AppointmentId.ShouldBe(appointment.Id);
        (await LoadAppointmentAsync(appointment.Id)).Status
            .ShouldBe(confirmed ? AppointmentStatus.Confirmed : AppointmentStatus.Scheduled);
    }

    [Fact]
    public async Task Handle_WithAppointmentStartedEarlierToday_PersistsConsultation()
    {
        var (patient, doctor) = await SeedPatientAndDoctorAsync();
        // 11:30 UTC = 06:30 local del mismo día
        var appointment = await AddAsync(CreateAppointment(patient.Id, doctor.Id, new DateTime(2026, 10, 26, 11, 30, 0, DateTimeKind.Utc)));
        ActAsDoctor(doctor.Id);

        var result = await HandleAsync(patient.Id.Value, appointment.Id.Value);

        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_WithAppointmentOfTodayInLocalTimeButTomorrowInUtc_PersistsConsultation()
    {
        var (patient, doctor) = await SeedPatientAndDoctorAsync();
        // 03:00 UTC del 27 = 22:00 local del 26
        var appointment = await AddAsync(CreateAppointment(patient.Id, doctor.Id, new DateTime(2026, 10, 27, 3, 0, 0, DateTimeKind.Utc)));
        ActAsDoctor(doctor.Id);

        var result = await HandleAsync(patient.Id.Value, appointment.Id.Value);

        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_AsUnlinkedDoctor_ThrowsForbiddenException()
    {
        _db.CurrentUser.Role.Returns(Role.Doctor);
        _db.CurrentUser.DoctorId.Returns((DoctorId?)null);

        var exception = await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(Guid.CreateVersion7()));

        exception.Message.ShouldBe("Your user is not linked to a doctor.");
    }

    [Fact]
    public async Task Handle_WithInactiveDoctor_ThrowsBadRequestException()
    {
        var (patient, doctor) = await SeedPatientAndDoctorAsync(doctorActive: false);
        ActAsDoctor(doctor.Id);

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(patient.Id.Value));

        exception.Message.ShouldBe("Doctor is inactive.");
    }

    [Fact]
    public async Task Handle_WithUnknownPatient_ThrowsNotFoundException()
    {
        var (_, doctor) = await SeedPatientAndDoctorAsync();
        ActAsDoctor(doctor.Id);

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Handle_WithInactivePatient_ThrowsBadRequestException()
    {
        var (patient, doctor) = await SeedPatientAndDoctorAsync(patientActive: false);
        ActAsDoctor(doctor.Id);

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(patient.Id.Value));

        exception.Message.ShouldBe("Patient is inactive.");
    }

    [Fact]
    public async Task Handle_WithUnknownAppointment_ThrowsNotFoundException()
    {
        var (patient, doctor) = await SeedPatientAndDoctorAsync();
        ActAsDoctor(doctor.Id);

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(patient.Id.Value, Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Handle_WithAppointmentOfOtherDoctor_ThrowsForbiddenException()
    {
        var (patient, doctor) = await SeedPatientAndDoctorAsync();
        var appointment = await AddAsync(CreateAppointment(patient.Id, DoctorId.New(), TodayLaterUtc));
        ActAsDoctor(doctor.Id);

        var exception = await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(patient.Id.Value, appointment.Id.Value));

        exception.Message.ShouldBe("You can only manage your own appointments.");
    }

    [Fact]
    public async Task Handle_WithAppointmentOfOtherPatient_ThrowsBadRequestException()
    {
        var (patient, doctor) = await SeedPatientAndDoctorAsync();
        var appointment = await AddAsync(CreateAppointment(PatientId.New(), doctor.Id, TodayLaterUtc));
        ActAsDoctor(doctor.Id);

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(patient.Id.Value, appointment.Id.Value));

        exception.Message.ShouldBe("Appointment does not belong to the patient.");
    }

    [Fact]
    public async Task Handle_WithCancelledAppointment_ThrowsBadRequestException()
    {
        var (patient, doctor) = await SeedPatientAndDoctorAsync();
        var appointment = CreateAppointment(patient.Id, doctor.Id, TodayLaterUtc);
        appointment.Cancel("Viaje", TestAppointments.NowUtc);
        await AddAsync(appointment);
        ActAsDoctor(doctor.Id);

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(patient.Id.Value, appointment.Id.Value));

        exception.Message.ShouldBe("Only scheduled or confirmed appointments can start a consultation.");
    }

    [Theory]
    [InlineData(2026, 10, 25, 15)] // ayer
    [InlineData(2026, 10, 26, 4)]  // 23:00 local de ayer, aunque en UTC sea el mismo día
    [InlineData(2026, 10, 27, 15)] // mañana
    public async Task Handle_WithAppointmentOfOtherDay_ThrowsBadRequestException(int year, int month, int day, int hourUtc)
    {
        var (patient, doctor) = await SeedPatientAndDoctorAsync();
        var appointment = await AddAsync(
            CreateAppointment(patient.Id, doctor.Id, new DateTime(year, month, day, hourUtc, 0, 0, DateTimeKind.Utc)));
        ActAsDoctor(doctor.Id);

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(patient.Id.Value, appointment.Id.Value));

        exception.Message.ShouldBe("Appointment is not scheduled for today.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_WithAppointmentThatAlreadyHasConsultation_ThrowsConflictException(bool closed)
    {
        var (patient, doctor) = await SeedPatientAndDoctorAsync();
        var appointment = await AddAsync(CreateAppointment(patient.Id, doctor.Id, TodayLaterUtc));
        var previous = TestConsultations.Start(patient.Id, doctor.Id, appointment.Id);
        if (closed)
            previous.Close("Cefalea tensional", null, TestConsultations.StartedAtUtc);
        await AddAsync(previous);
        ActAsDoctor(doctor.Id);

        var exception = await Should.ThrowAsync<ConflictException>(() => HandleAsync(patient.Id.Value, appointment.Id.Value));

        exception.Message.ShouldBe("The appointment already has a consultation.");
    }

    public void Dispose() => _db.Dispose();

    // nowUtc anterior al inicio para poder crear también citas ya empezadas o de días pasados
    private static Appointment CreateAppointment(PatientId patientId, DoctorId doctorId, DateTime startUtc) =>
        Appointment.Create(patientId, doctorId, startUtc, startUtc.AddMinutes(30), "Control", startUtc.AddDays(-1));

    private void ActAsDoctor(DoctorId doctorId)
    {
        _db.CurrentUser.Role.Returns(Role.Doctor);
        _db.CurrentUser.DoctorId.Returns(doctorId);
    }

    private async Task<(Patient Patient, Doctor Doctor)> SeedPatientAndDoctorAsync(
        bool patientActive = true,
        bool doctorActive = true)
    {
        var patient = TestPatients.Create();
        var doctor = TestDoctors.Create();
        if (!patientActive)
            patient.Deactivate();
        if (!doctorActive)
            doctor.Deactivate();

        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        context.Doctors.Add(doctor);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (patient, doctor);
    }

    private async Task<Appointment> AddAsync(Appointment appointment)
    {
        await using var context = _db.CreateContext();
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return appointment;
    }

    private async Task AddAsync(Consultation consultation)
    {
        await using var context = _db.CreateContext();
        context.Consultations.Add(consultation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Consultation> LoadAsync(ConsultationId id)
    {
        await using var context = _db.CreateContext();
        return await context.Consultations.SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<Appointment> LoadAppointmentAsync(AppointmentId id)
    {
        await using var context = _db.CreateContext();
        return await context.Appointments.SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<StartConsultationResult> HandleAsync(Guid patientId, Guid? appointmentId = null, string reason = "Dolor de cabeza")
    {
        await using var context = _db.CreateContext();
        return await new StartConsultationHandler(context, _db.CurrentUser, _db.ClinicTimeZone, _db.TimeProvider)
            .Handle(new StartConsultationCommand(patientId, appointmentId, reason), TestContext.Current.CancellationToken);
    }
}
