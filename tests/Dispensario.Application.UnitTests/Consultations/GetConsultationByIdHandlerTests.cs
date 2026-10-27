using BuildingBlocks.Exceptions;
using Dispensario.Application.Consultations.Dtos;
using Dispensario.Application.Consultations.Queries.GetConsultationById;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Consultations;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Dispensario.Domain.Users;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Consultations;

public sealed class GetConsultationByIdHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithOpenConsultation_ReturnsFullDto()
    {
        var (patient, doctor, consultation) = await SeedAsync();
        ActAsDoctor(doctor.Id);

        var dto = await HandleAsync(consultation.Id.Value);

        dto.Id.ShouldBe(consultation.Id.Value);
        dto.PatientId.ShouldBe(patient.Id.Value);
        dto.PatientFullName.ShouldBe("Ana Pérez");
        dto.DoctorId.ShouldBe(doctor.Id.Value);
        dto.DoctorFullName.ShouldBe("Luis Mora");
        dto.AppointmentId.ShouldBeNull();
        dto.Reason.ShouldBe("Dolor de cabeza");
        dto.Diagnosis.ShouldBeNull();
        dto.Notes.ShouldBeNull();
        dto.Status.ShouldBe(ConsultationStatus.Open);
        dto.StartedAtUtc.ShouldBe(new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc));
        dto.StartedAtLocal.ShouldBe(new DateTime(2026, 10, 26, 7, 0, 0));
        dto.StartedAtLocal.Kind.ShouldBe(DateTimeKind.Unspecified);
        dto.ClosedAtUtc.ShouldBeNull();
        dto.ClosedAtLocal.ShouldBeNull();
        dto.CreatedAtUtc.ShouldBe(DispensarioTestDb.StartTime.UtcDateTime);
    }

    [Fact]
    public async Task Handle_WithClosedConsultationWithAppointment_ReturnsClosedTimesAndAppointment()
    {
        var appointmentId = AppointmentId.New();
        var (_, doctor, consultation) = await SeedAsync(appointmentId, close: true);
        ActAsDoctor(doctor.Id);

        var dto = await HandleAsync(consultation.Id.Value);

        dto.AppointmentId.ShouldBe(appointmentId.Value);
        dto.Status.ShouldBe(ConsultationStatus.Closed);
        dto.Diagnosis.ShouldBe("Cefalea tensional");
        dto.Notes.ShouldBe("Control en dos semanas.");
        dto.ClosedAtUtc.ShouldBe(new DateTime(2026, 10, 26, 12, 30, 0, DateTimeKind.Utc));
        dto.ClosedAtLocal.ShouldBe(new DateTime(2026, 10, 26, 7, 30, 0));
    }

    [Fact]
    public async Task Handle_AsOtherLinkedDoctor_ReturnsDto()
    {
        var (_, _, consultation) = await SeedAsync();
        ActAsDoctor(DoctorId.New());

        var dto = await HandleAsync(consultation.Id.Value);

        dto.Id.ShouldBe(consultation.Id.Value);
    }

    [Fact]
    public async Task Handle_AsAdmin_ReturnsDto()
    {
        var (_, _, consultation) = await SeedAsync();
        _db.CurrentUser.Role.Returns(Role.Admin);

        var dto = await HandleAsync(consultation.Id.Value);

        dto.Id.ShouldBe(consultation.Id.Value);
    }

    [Fact]
    public async Task Handle_AsUnlinkedDoctor_ThrowsForbiddenException()
    {
        ActAsDoctor(null);

        // Antes de leer la BD: con una consulta inexistente también sale 403
        var exception = await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(Guid.CreateVersion7()));

        exception.Message.ShouldBe("Your user is not linked to a doctor.");
    }

    [Fact]
    public async Task Handle_WithUnknownId_ThrowsNotFoundException()
    {
        _db.CurrentUser.Role.Returns(Role.Admin);

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Guid.CreateVersion7()));
    }

    public void Dispose() => _db.Dispose();

    private void ActAsDoctor(DoctorId? doctorId)
    {
        _db.CurrentUser.Role.Returns(Role.Doctor);
        _db.CurrentUser.DoctorId.Returns(doctorId);
    }

    private async Task<(Patient Patient, Doctor Doctor, Consultation Consultation)> SeedAsync(
        AppointmentId? appointmentId = null,
        bool close = false)
    {
        var patient = TestPatients.Create();
        var doctor = TestDoctors.Create();
        var consultation = TestConsultations.Start(patient.Id, doctor.Id, appointmentId);
        if (close)
            consultation.Close("Cefalea tensional", "Control en dos semanas.", TestConsultations.StartedAtUtc.AddMinutes(30));

        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        context.Doctors.Add(doctor);
        context.Consultations.Add(consultation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (patient, doctor, consultation);
    }

    private async Task<ConsultationDto> HandleAsync(Guid id)
    {
        await using var context = _db.CreateContext();
        return await new GetConsultationByIdHandler(context, _db.CurrentUser, _db.ClinicTimeZone)
            .Handle(new GetConsultationByIdQuery(id), TestContext.Current.CancellationToken);
    }
}
