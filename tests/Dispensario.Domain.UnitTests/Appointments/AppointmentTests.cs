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

    [Fact]
    public void Confirm_WhenScheduled_SetsConfirmed()
    {
        var appointment = CreateAppointment();

        appointment.Confirm(NowUtc);

        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
        appointment.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Confirm_WhenNotScheduled_ThrowsDomainException(AppointmentStatus status)
    {
        var appointment = CreateAppointmentIn(status);

        var act = () => appointment.Confirm(NowUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Only scheduled appointments can be confirmed.");
        appointment.Status.ShouldBe(status);
    }

    [Fact]
    public void Confirm_WhenAlreadyStarted_ThrowsDomainException()
    {
        var appointment = CreateAppointment();

        var act = () => appointment.Confirm(StartUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Appointment has already started.");
        appointment.Status.ShouldBe(AppointmentStatus.Scheduled);
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Confirmed)]
    public void Cancel_WhenPending_SetsCancelledWithTrimmedReason(AppointmentStatus status)
    {
        var appointment = CreateAppointmentIn(status);

        appointment.Cancel("  Viaje  ", NowUtc);

        appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
        appointment.CancellationReason.ShouldBe("Viaje");
        appointment.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Cancel_WhenNotPending_ThrowsDomainException(AppointmentStatus status)
    {
        var appointment = CreateAppointmentIn(status);

        var act = () => appointment.Cancel("Viaje", NowUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Only scheduled or confirmed appointments can be cancelled.");
        appointment.Status.ShouldBe(status);
    }

    [Fact]
    public void Cancel_WhenAlreadyStarted_ThrowsDomainException()
    {
        var appointment = CreateAppointment();

        var act = () => appointment.Cancel("Viaje", StartUtc.AddMinutes(1));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Appointment has already started.");
        appointment.Status.ShouldBe(AppointmentStatus.Scheduled);
        appointment.CancellationReason.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Cancel_WithBlankReason_ThrowsDomainException(string? reason)
    {
        var appointment = CreateAppointment();

        var act = () => appointment.Cancel(reason!, NowUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Cancellation reason is required.");
        appointment.Status.ShouldBe(AppointmentStatus.Scheduled);
    }

    [Fact]
    public void Cancel_WithTooLongReason_ThrowsDomainException()
    {
        var appointment = CreateAppointment();

        var act = () => appointment.Cancel(new string('a', Appointment.CancellationReasonMaxLength + 1), NowUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Cancellation reason must be at most 500 characters.");
        appointment.Status.ShouldBe(AppointmentStatus.Scheduled);
    }

    [Fact]
    public void Cancel_WithReasonAtMaxLength_SetsCancelled()
    {
        var appointment = CreateAppointment();

        appointment.Cancel(new string('a', Appointment.CancellationReasonMaxLength), NowUtc);

        appointment.CancellationReason!.Length.ShouldBe(Appointment.CancellationReasonMaxLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    public void MarkNoShow_FromStart_SetsNoShow(int minutesAfterStart)
    {
        var appointment = CreateAppointmentIn(AppointmentStatus.Confirmed);

        appointment.MarkNoShow(StartUtc.AddMinutes(minutesAfterStart));

        appointment.Status.ShouldBe(AppointmentStatus.NoShow);
        appointment.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void MarkNoShow_BeforeStart_ThrowsDomainException()
    {
        var appointment = CreateAppointment();

        var act = () => appointment.MarkNoShow(StartUtc.AddMinutes(-1));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Appointment has not started yet.");
        appointment.Status.ShouldBe(AppointmentStatus.Scheduled);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void MarkNoShow_WhenNotPending_ThrowsDomainException(AppointmentStatus status)
    {
        var appointment = CreateAppointmentIn(status);

        var act = () => appointment.MarkNoShow(StartUtc.AddMinutes(10));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Only scheduled or confirmed appointments can be marked as no-show.");
        appointment.Status.ShouldBe(status);
    }

    [Fact]
    public void Reschedule_WhenConfirmed_SetsNewTimesAndScheduled()
    {
        var appointment = CreateAppointmentIn(AppointmentStatus.Confirmed);
        var newStart = StartUtc.AddDays(1);

        appointment.Reschedule(newStart, newStart.AddMinutes(45), NowUtc);

        appointment.StartUtc.ShouldBe(newStart);
        appointment.EndUtc.ShouldBe(newStart.AddMinutes(45));
        appointment.Status.ShouldBe(AppointmentStatus.Scheduled);
        appointment.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Reschedule_WithSameStart_ThrowsDomainException()
    {
        var appointment = CreateAppointmentIn(AppointmentStatus.Confirmed);

        var act = () => appointment.Reschedule(StartUtc, EndUtc, NowUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("New start must be different from the current one.");
        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public void Reschedule_WithStartNotBeforeEnd_ThrowsDomainException()
    {
        var appointment = CreateAppointment();
        var newStart = StartUtc.AddDays(1);

        var act = () => appointment.Reschedule(newStart, newStart, NowUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Appointment start must be before its end.");
        appointment.StartUtc.ShouldBe(StartUtc);
    }

    [Fact]
    public void Reschedule_ToThePast_ThrowsDomainException()
    {
        var appointment = CreateAppointment();
        var newStart = NowUtc.AddHours(-1);

        var act = () => appointment.Reschedule(newStart, newStart.AddMinutes(30), NowUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Appointment cannot be scheduled in the past.");
        appointment.StartUtc.ShouldBe(StartUtc);
        appointment.EndUtc.ShouldBe(EndUtc);
    }

    [Fact]
    public void Reschedule_WhenAlreadyStarted_ThrowsDomainException()
    {
        var appointment = CreateAppointment();
        var newStart = StartUtc.AddDays(1);

        var act = () => appointment.Reschedule(newStart, newStart.AddMinutes(30), StartUtc.AddMinutes(5));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Appointment has already started.");
        appointment.StartUtc.ShouldBe(StartUtc);
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void Reschedule_WhenNotPending_ThrowsDomainException(AppointmentStatus status)
    {
        var appointment = CreateAppointmentIn(status);
        var newStart = StartUtc.AddDays(1);

        var act = () => appointment.Reschedule(newStart, newStart.AddMinutes(30), NowUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Only scheduled or confirmed appointments can be rescheduled.");
        appointment.Status.ShouldBe(status);
        appointment.StartUtc.ShouldBe(StartUtc);
    }

    [Fact]
    public void Complete_WhenScheduled_SetsCompleted()
    {
        var appointment = CreateAppointment();

        appointment.Complete();

        appointment.Status.ShouldBe(AppointmentStatus.Completed);
        appointment.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_WhenConfirmed_SetsCompleted()
    {
        var appointment = CreateAppointmentIn(AppointmentStatus.Confirmed);

        appointment.Complete();

        appointment.Status.ShouldBe(AppointmentStatus.Completed);
    }

    [Fact]
    public void Complete_WhenCancelled_ThrowsDomainException() =>
        AssertCompleteThrows(AppointmentStatus.Cancelled);

    [Fact]
    public void Complete_WhenNoShow_ThrowsDomainException() =>
        AssertCompleteThrows(AppointmentStatus.NoShow);

    [Fact]
    public void Complete_WhenCompleted_ThrowsDomainException() =>
        AssertCompleteThrows(AppointmentStatus.Completed);

    // CreateAppointment deja la cita en el futuro respecto a NowUtc: Complete no mira la hora
    [Fact]
    public void Complete_BeforeStart_SetsCompleted()
    {
        var appointment = CreateAppointment();
        appointment.StartUtc.ShouldBeGreaterThan(NowUtc);

        appointment.Complete();

        appointment.Status.ShouldBe(AppointmentStatus.Completed);
    }

    [Fact]
    public void Complete_DoesNotChangeTimesOrReason()
    {
        var appointment = CreateAppointment();

        appointment.Complete();

        appointment.StartUtc.ShouldBe(StartUtc);
        appointment.EndUtc.ShouldBe(EndUtc);
        appointment.Reason.ShouldBe("Control");
        appointment.CancellationReason.ShouldBeNull();
    }

    [Fact]
    public void Confirm_WhenCompleted_ThrowsDomainException()
    {
        var appointment = CreateAppointmentIn(AppointmentStatus.Completed);

        var act = () => appointment.Confirm(NowUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Only scheduled appointments can be confirmed.");
        appointment.Status.ShouldBe(AppointmentStatus.Completed);
    }

    [Fact]
    public void Cancel_WhenCompleted_ThrowsDomainException()
    {
        var appointment = CreateAppointmentIn(AppointmentStatus.Completed);

        var act = () => appointment.Cancel("Viaje", NowUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Only scheduled or confirmed appointments can be cancelled.");
        appointment.Status.ShouldBe(AppointmentStatus.Completed);
        appointment.CancellationReason.ShouldBeNull();
    }

    [Fact]
    public void MarkNoShow_WhenCompleted_ThrowsDomainException()
    {
        var appointment = CreateAppointmentIn(AppointmentStatus.Completed);

        var act = () => appointment.MarkNoShow(StartUtc.AddMinutes(10));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Only scheduled or confirmed appointments can be marked as no-show.");
        appointment.Status.ShouldBe(AppointmentStatus.Completed);
    }

    [Fact]
    public void Reschedule_WhenCompleted_ThrowsDomainException()
    {
        var appointment = CreateAppointmentIn(AppointmentStatus.Completed);
        var newStart = StartUtc.AddDays(1);

        var act = () => appointment.Reschedule(newStart, newStart.AddMinutes(30), NowUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Only scheduled or confirmed appointments can be rescheduled.");
        appointment.Status.ShouldBe(AppointmentStatus.Completed);
        appointment.StartUtc.ShouldBe(StartUtc);
    }

    private static void AssertCompleteThrows(AppointmentStatus status)
    {
        var appointment = CreateAppointmentIn(status);

        var act = appointment.Complete;

        act.ShouldThrow<DomainException>().Message.ShouldBe("Only scheduled or confirmed appointments can be completed.");
        appointment.Status.ShouldBe(status);
    }

    // Lleva la cita al estado pedido con las transiciones del dominio
    private static Appointment CreateAppointmentIn(AppointmentStatus status)
    {
        var appointment = CreateAppointment();

        switch (status)
        {
            case AppointmentStatus.Confirmed:
                appointment.Confirm(NowUtc);
                break;
            case AppointmentStatus.Cancelled:
                appointment.Cancel("Viaje", NowUtc);
                break;
            case AppointmentStatus.NoShow:
                appointment.MarkNoShow(StartUtc);
                break;
            case AppointmentStatus.Completed:
                appointment.Complete();
                break;
        }

        return appointment;
    }

    private static Appointment CreateAppointment(
        DateTime? startUtc = null,
        DateTime? endUtc = null,
        string reason = "Control") =>
        Appointment.Create(PatientId.New(), DoctorId.New(), startUtc ?? StartUtc, endUtc ?? EndUtc, reason, NowUtc);
}
