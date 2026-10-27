using BuildingBlocks.Domain;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Consultations;
using Dispensario.Domain.Consultations.Events;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;

namespace Dispensario.Domain.UnitTests.Consultations;

public sealed class ConsultationTests
{
    private static readonly DateTime StartedAtUtc = new(2026, 10, 27, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ClosedAtUtc = StartedAtUtc.AddMinutes(25);

    [Fact]
    public void Start_WithValidData_ReturnsOpenConsultation()
    {
        var patientId = PatientId.New();
        var doctorId = DoctorId.New();
        var appointmentId = AppointmentId.New();

        var consultation = Consultation.Start(patientId, doctorId, appointmentId, "Dolor de cabeza", StartedAtUtc);

        consultation.Id.Value.ShouldNotBe(Guid.Empty);
        consultation.PatientId.ShouldBe(patientId);
        consultation.DoctorId.ShouldBe(doctorId);
        consultation.AppointmentId.ShouldBe(appointmentId);
        consultation.Reason.ShouldBe("Dolor de cabeza");
        consultation.Diagnosis.ShouldBeNull();
        consultation.Notes.ShouldBeNull();
        consultation.Status.ShouldBe(ConsultationStatus.Open);
        consultation.StartedAtUtc.ShouldBe(StartedAtUtc);
        consultation.ClosedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void Start_WithoutAppointment_LeavesAppointmentIdNull()
    {
        var consultation = StartConsultation(appointmentId: null);

        consultation.AppointmentId.ShouldBeNull();
    }

    [Fact]
    public void Start_WithReasonSurroundedBySpaces_TrimsReason()
    {
        var consultation = StartConsultation(reason: "  Dolor de cabeza  ");

        consultation.Reason.ShouldBe("Dolor de cabeza");
    }

    [Fact]
    public void Start_WithValidData_RaisesNoDomainEvents()
    {
        var consultation = StartConsultation();

        consultation.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Start_WithBlankReason_ThrowsDomainException(string? reason)
    {
        var act = () => StartConsultation(reason: reason!);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Reason is required.");
    }

    [Fact]
    public void Start_WithReasonTooLong_ThrowsDomainException()
    {
        var act = () => StartConsultation(reason: new string('a', Consultation.ReasonMaxLength + 1));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Reason must be at most 500 characters.");
    }

    [Fact]
    public void Update_WhenOpen_ReplacesTexts()
    {
        var consultation = StartConsultation();

        consultation.Update("  Cefalea  ", "  Cefalea tensional  ", "  Sin signos de alarma.  ");

        consultation.Reason.ShouldBe("Cefalea");
        consultation.Diagnosis.ShouldBe("Cefalea tensional");
        consultation.Notes.ShouldBe("Sin signos de alarma.");
        consultation.Status.ShouldBe(ConsultationStatus.Open);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Update_WithBlankOptionals_ClearsValues(string? value)
    {
        var consultation = StartConsultation();
        consultation.Update("Cefalea", "Cefalea tensional", "Notas");

        consultation.Update("Cefalea", value, value);

        consultation.Diagnosis.ShouldBeNull();
        consultation.Notes.ShouldBeNull();
    }

    [Fact]
    public void Update_WhenClosed_ThrowsDomainException()
    {
        var consultation = StartConsultation();
        consultation.Close("Cefalea tensional", null, ClosedAtUtc);

        var act = () => consultation.Update("Otro motivo", null, null);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Only open consultations can be updated.");
        consultation.Reason.ShouldBe("Dolor de cabeza");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Update_WithBlankReason_ThrowsDomainException(string? reason)
    {
        var consultation = StartConsultation();

        var act = () => consultation.Update(reason!, null, null);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Reason is required.");
    }

    [Fact]
    public void Update_WithReasonTooLong_ThrowsDomainException()
    {
        var consultation = StartConsultation();

        var act = () => consultation.Update(new string('a', Consultation.ReasonMaxLength + 1), null, null);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Reason must be at most 500 characters.");
    }

    [Fact]
    public void Update_WithDiagnosisTooLong_ThrowsDomainExceptionAndKeepsValues()
    {
        var consultation = StartConsultation();

        var act = () => consultation.Update("Otro motivo", new string('a', Consultation.DiagnosisMaxLength + 1), "Notas");

        act.ShouldThrow<DomainException>().Message.ShouldBe("Diagnosis must be at most 1000 characters.");
        consultation.Reason.ShouldBe("Dolor de cabeza");
        consultation.Notes.ShouldBeNull();
    }

    [Fact]
    public void Update_WithNotesTooLong_ThrowsDomainException()
    {
        var consultation = StartConsultation();

        var act = () => consultation.Update("Cefalea", null, new string('a', Consultation.NotesMaxLength + 1));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Notes must be at most 4000 characters.");
    }

    [Fact]
    public void Close_WhenOpen_ClosesWithDiagnosisNotesAndClosedAt()
    {
        var consultation = StartConsultation();

        consultation.Close("  Cefalea tensional  ", "  Control en dos semanas.  ", ClosedAtUtc);

        consultation.Status.ShouldBe(ConsultationStatus.Closed);
        consultation.Diagnosis.ShouldBe("Cefalea tensional");
        consultation.Notes.ShouldBe("Control en dos semanas.");
        consultation.ClosedAtUtc.ShouldBe(ClosedAtUtc);
    }

    [Fact]
    public void Close_WithAppointment_RaisesConsultationClosedDomainEvent()
    {
        var appointmentId = AppointmentId.New();
        var consultation = StartConsultation(appointmentId: appointmentId);

        consultation.Close("Cefalea tensional", null, ClosedAtUtc);

        var domainEvent = consultation.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ConsultationClosedDomainEvent>();
        domainEvent.ConsultationId.ShouldBe(consultation.Id);
        domainEvent.AppointmentId.ShouldBe(appointmentId);
    }

    [Fact]
    public void Close_WithoutAppointment_RaisesConsultationClosedDomainEventWithNullAppointment()
    {
        var consultation = StartConsultation(appointmentId: null);

        consultation.Close("Cefalea tensional", null, ClosedAtUtc);

        var domainEvent = consultation.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ConsultationClosedDomainEvent>();
        domainEvent.ConsultationId.ShouldBe(consultation.Id);
        domainEvent.AppointmentId.ShouldBeNull();
    }

    [Fact]
    public void Close_WhenRuleFails_RaisesNoDomainEvents()
    {
        var withoutDiagnosis = StartConsultation();
        var alreadyClosed = StartConsultation();
        alreadyClosed.Close("Cefalea tensional", null, ClosedAtUtc);
        alreadyClosed.ClearDomainEvents();

        Should.Throw<DomainException>(() => withoutDiagnosis.Close(" ", null, ClosedAtUtc));
        Should.Throw<DomainException>(() => alreadyClosed.Close("Otro diagnóstico", null, ClosedAtUtc));

        withoutDiagnosis.DomainEvents.ShouldBeEmpty();
        alreadyClosed.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Update_RaisesNoDomainEvents()
    {
        var consultation = StartConsultation();

        consultation.Update("Cefalea", "Cefalea tensional", null);

        consultation.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Close_WithNullNotes_ClearsNotes()
    {
        var consultation = StartConsultation();
        consultation.Update("Cefalea", null, "Notas previas");

        consultation.Close("Cefalea tensional", null, ClosedAtUtc);

        consultation.Notes.ShouldBeNull();
    }

    [Fact]
    public void Close_AtStartInstant_Closes()
    {
        var consultation = StartConsultation();

        consultation.Close("Cefalea tensional", null, StartedAtUtc);

        consultation.ClosedAtUtc.ShouldBe(StartedAtUtc);
    }

    [Fact]
    public void Close_WhenAlreadyClosed_ThrowsDomainException()
    {
        var consultation = StartConsultation();
        consultation.Close("Cefalea tensional", null, ClosedAtUtc);

        var act = () => consultation.Close("Otro diagnóstico", null, ClosedAtUtc.AddMinutes(5));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Consultation is already closed.");
        consultation.Diagnosis.ShouldBe("Cefalea tensional");
        consultation.ClosedAtUtc.ShouldBe(ClosedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Close_WithBlankDiagnosis_ThrowsDomainExceptionAndStaysOpen(string? diagnosis)
    {
        var consultation = StartConsultation();

        var act = () => consultation.Close(diagnosis!, null, ClosedAtUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Diagnosis is required to close a consultation.");
        consultation.Status.ShouldBe(ConsultationStatus.Open);
        consultation.ClosedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void Close_WithDiagnosisTooLong_ThrowsDomainException()
    {
        var consultation = StartConsultation();

        var act = () => consultation.Close(new string('a', Consultation.DiagnosisMaxLength + 1), null, ClosedAtUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Diagnosis must be at most 1000 characters.");
    }

    [Fact]
    public void Close_WithNotesTooLong_ThrowsDomainException()
    {
        var consultation = StartConsultation();

        var act = () => consultation.Close("Cefalea", new string('a', Consultation.NotesMaxLength + 1), ClosedAtUtc);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Notes must be at most 4000 characters.");
    }

    [Fact]
    public void Close_BeforeStart_ThrowsDomainExceptionAndStaysOpen()
    {
        var consultation = StartConsultation();

        var act = () => consultation.Close("Cefalea tensional", "Notas", StartedAtUtc.AddMinutes(-1));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Consultation cannot be closed before it started.");
        consultation.Status.ShouldBe(ConsultationStatus.Open);
        consultation.Diagnosis.ShouldBeNull();
        consultation.Notes.ShouldBeNull();
    }

    private static Consultation StartConsultation(
        string reason = "Dolor de cabeza",
        AppointmentId? appointmentId = null) =>
        Consultation.Start(PatientId.New(), DoctorId.New(), appointmentId, reason, StartedAtUtc);
}
