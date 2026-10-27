using BuildingBlocks.Exceptions;
using Dispensario.Application.Appointments.Queries.GetAppointmentById;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Dispensario.Domain.Users;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Appointments;

public sealed class GetAppointmentByIdHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithExistingAppointment_ReturnsFullDto()
    {
        var (patient, doctor, appointment) = await SeedAsync();

        var dto = await HandleAsync(appointment.Id.Value);

        dto.Id.ShouldBe(appointment.Id.Value);
        dto.PatientId.ShouldBe(patient.Id.Value);
        dto.PatientFullName.ShouldBe("Ana Pérez");
        dto.DoctorId.ShouldBe(doctor.Id.Value);
        dto.DoctorFullName.ShouldBe("Luis Mora");
        dto.StartUtc.ShouldBe(new DateTime(2026, 11, 3, 14, 30, 0, DateTimeKind.Utc));
        dto.EndUtc.ShouldBe(new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc));
        dto.StartLocal.ShouldBe(new DateTime(2026, 11, 3, 9, 30, 0));
        dto.StartLocal.Kind.ShouldBe(DateTimeKind.Unspecified);
        dto.EndLocal.ShouldBe(new DateTime(2026, 11, 3, 10, 0, 0));
        dto.Reason.ShouldBe("Control");
        dto.Status.ShouldBe(AppointmentStatus.Scheduled);
        dto.CreatedAtUtc.ShouldBe(DispensarioTestDb.StartTime.UtcDateTime);
    }

    [Fact]
    public async Task Handle_WithUnknownId_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Handle_AsDoctorOwningAppointment_ReturnsDto()
    {
        var (_, doctor, appointment) = await SeedAsync();
        ActAsDoctor(doctor.Id);

        var dto = await HandleAsync(appointment.Id.Value);

        dto.Id.ShouldBe(appointment.Id.Value);
    }

    [Fact]
    public async Task Handle_AsOtherDoctor_ThrowsForbiddenException()
    {
        var (_, _, appointment) = await SeedAsync();
        ActAsDoctor(DoctorId.New());

        var exception = await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(appointment.Id.Value));

        exception.Message.ShouldBe("You can only view your own appointments.");
    }

    [Fact]
    public async Task Handle_AsUnlinkedDoctor_ThrowsForbiddenException()
    {
        var (_, _, appointment) = await SeedAsync();
        ActAsDoctor(null);

        var exception = await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(appointment.Id.Value));

        exception.Message.ShouldBe("Your user is not linked to a doctor.");
    }

    public void Dispose() => _db.Dispose();

    private void ActAsDoctor(DoctorId? doctorId)
    {
        _db.CurrentUser.Role.Returns(Role.Doctor);
        _db.CurrentUser.DoctorId.Returns(doctorId);
    }

    private async Task<(Patient Patient, Doctor Doctor, Appointment Appointment)> SeedAsync()
    {
        var patient = TestPatients.Create();
        var doctor = TestDoctors.Create();
        var appointment = TestAppointments.Create(patient.Id, doctor.Id);

        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        context.Doctors.Add(doctor);
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (patient, doctor, appointment);
    }

    private async Task<Application.Appointments.Dtos.AppointmentDto> HandleAsync(Guid id)
    {
        await using var context = _db.CreateContext();
        return await new GetAppointmentByIdHandler(context, _db.CurrentUser, _db.ClinicTimeZone)
            .Handle(new GetAppointmentByIdQuery(id), TestContext.Current.CancellationToken);
    }
}
