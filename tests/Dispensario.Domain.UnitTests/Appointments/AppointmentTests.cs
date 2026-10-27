using BuildingBlocks.Domain;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;

namespace Dispensario.Domain.UnitTests.Appointments;

public sealed class AppointmentTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime StartUtc = new(2026, 11, 3, 14, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime EndUtc = StartUtc.AddMinutes(30);

    [Fact]
    public void Create_WithValidData_ReturnsScheduledAppointment()
    {
        var patientId = PatientId.New();
        var doctorId = DoctorId.New();

        var appointment = Appointment.Create(patientId, doctorId, StartUtc, EndUtc, "Control", NowUtc);

        appointment.Id.Value.ShouldNotBe(Guid.Empty);
        appointment.PatientId.ShouldBe(patientId);
        appointment.DoctorId.ShouldBe(doctorId);
        appointment.StartUtc.ShouldBe(StartUtc);
        appointment.EndUtc.ShouldBe(EndUtc);
        appointment.Reason.ShouldBe("Control");
        appointment.Status.ShouldBe(AppointmentStatus.Scheduled);
    }

    [Fact]
    public void Create_WithValidData_RaisesNoDomainEvents()
    {
        var appointment = CreateAppointment();

        appointment.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Create_WithReasonSurroundedBySpaces_TrimsReason()
    {
        var appointment = CreateAppointment(reason: "  Control  ");

        appointment.Reason.ShouldBe("Control");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Create_WithStartNotBeforeEnd_ThrowsDomainException(int durationMinutes)
    {
        var act = () => CreateAppointment(endUtc: StartUtc.AddMinutes(durationMinutes));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Appointment start must be before its end.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithStartNotAfterNow_ThrowsDomainException(int minutesFromNow)
    {
        var start = NowUtc.AddMinutes(minutesFromNow);

        var act = () => CreateAppointment(startUtc: start, endUtc: start.AddMinutes(30));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Appointment cannot be scheduled in the past.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithBlankReason_ThrowsDomainException(string? reason)
    {
        var act = () => CreateAppointment(reason: reason!);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Reason is required.");
    }

    [Fact]
    public void Create_WithTooLongReason_ThrowsDomainException()
    {
        var act = () => CreateAppointment(reason: new string('a', Appointment.ReasonMaxLength + 1));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Reason must be at most 500 characters.");
    }

    [Fact]
    public void Create_WithReasonAtMaxLength_ReturnsAppointment()
    {
        var appointment = CreateAppointment(reason: new string('a', Appointment.ReasonMaxLength));

        appointment.Reason.Length.ShouldBe(Appointment.ReasonMaxLength);
    }

    private static Appointment CreateAppointment(
        DateTime? startUtc = null,
        DateTime? endUtc = null,
        string reason = "Control") =>
        Appointment.Create(PatientId.New(), DoctorId.New(), startUtc ?? StartUtc, endUtc ?? EndUtc, reason, NowUtc);
}
