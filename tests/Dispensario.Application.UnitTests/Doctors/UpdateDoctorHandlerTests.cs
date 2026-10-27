using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Doctors.Commands.UpdateDoctor;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Doctors;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Doctors;

public sealed class UpdateDoctorHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithNewData_UpdatesAllFields()
    {
        var doctor = await AddAsync(TestDoctors.Create(phone: "0991234567"));

        await HandleAsync(new UpdateDoctorCommand(
            doctor.Id.Value, "Beatriz", "Andrade", "cm-9", "Cardiología", "bea@dispensario.local", null, 45));

        var loaded = await LoadAsync(doctor.Id);
        loaded.FullName.FirstName.ShouldBe("Beatriz");
        loaded.FullName.LastName.ShouldBe("Andrade");
        loaded.LicenseNumber.ShouldBe("CM-9");
        loaded.Specialty.ShouldBe("Cardiología");
        loaded.Email.Value.ShouldBe("bea@dispensario.local");
        loaded.Phone.ShouldBeNull();
        loaded.AppointmentDurationMinutes.ShouldBe(45);
    }

    [Fact]
    public async Task Handle_WithoutDuration_KeepsCurrentDuration()
    {
        var doctor = await AddAsync(TestDoctors.Create(appointmentDurationMinutes: 20));

        await HandleAsync(CommandFor(doctor.Id, doctor.LicenseNumber, duration: null));

        (await LoadAsync(doctor.Id)).AppointmentDurationMinutes.ShouldBe(20);
    }

    [Fact]
    public async Task Handle_KeepingOwnLicenseNumber_DoesNotConflict()
    {
        var doctor = await AddAsync(TestDoctors.Create(licenseNumber: "MP-123"));

        await HandleAsync(CommandFor(doctor.Id, "mp-123"));

        (await LoadAsync(doctor.Id)).LicenseNumber.ShouldBe("MP-123");
    }

    [Fact]
    public async Task Handle_WithLicenseOfOtherDoctor_ThrowsConflictException()
    {
        await AddAsync(TestDoctors.Create(licenseNumber: "MP-111"));
        var doctor = await AddAsync(TestDoctors.Create(licenseNumber: "MP-222"));

        await Should.ThrowAsync<ConflictException>(() => HandleAsync(CommandFor(doctor.Id, "mp-111")));

        (await LoadAsync(doctor.Id)).LicenseNumber.ShouldBe("MP-222");
    }

    [Fact]
    public async Task Handle_WithUnknownDoctor_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(CommandFor(DoctorId.New(), "MP-123")));
    }

    [Fact]
    public async Task Handle_WithInactiveDoctor_UpdatesData()
    {
        var doctor = TestDoctors.Create();
        doctor.Deactivate();
        await AddAsync(doctor);

        await HandleAsync(CommandFor(doctor.Id, doctor.LicenseNumber, specialty: "Dermatología"));

        (await LoadAsync(doctor.Id)).Specialty.ShouldBe("Dermatología");
    }

    [Fact]
    public async Task Handle_WithDurationLongerThanScheduleBlock_ThrowsDomainException()
    {
        var doctor = TestDoctors.Create(appointmentDurationMinutes: 30);
        doctor.SetWeeklySchedule([ScheduleBlock.Create(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 30))]);
        await AddAsync(doctor);

        var act = () => HandleAsync(CommandFor(doctor.Id, doctor.LicenseNumber, duration: 45));

        (await act.ShouldThrowAsync<DomainException>()).Message
            .ShouldBe("Appointment duration cannot be longer than a schedule block.");
        (await LoadAsync(doctor.Id)).AppointmentDurationMinutes.ShouldBe(30);
    }

    public void Dispose() => _db.Dispose();

    private static UpdateDoctorCommand CommandFor(
        DoctorId id, string licenseNumber, int? duration = 30, string specialty = "Pediatría") =>
        new(id.Value, "Luis", "Mora", licenseNumber, specialty, "luis.mora@dispensario.local", null, duration);

    private async Task<Doctor> AddAsync(Doctor doctor)
    {
        await using var context = _db.CreateContext();
        context.Doctors.Add(doctor);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return doctor;
    }

    private async Task<Doctor> LoadAsync(DoctorId doctorId)
    {
        await using var context = _db.CreateContext();
        return await context.Doctors.SingleAsync(d => d.Id == doctorId, TestContext.Current.CancellationToken);
    }

    private async Task HandleAsync(UpdateDoctorCommand command)
    {
        await using var context = _db.CreateContext();
        await new UpdateDoctorHandler(context).Handle(command, TestContext.Current.CancellationToken);
    }
}
