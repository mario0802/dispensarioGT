using BuildingBlocks.Exceptions;
using Dispensario.Application.Doctors.Commands.CreateDoctor;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Doctors;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Doctors;

public sealed class CreateDoctorHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithValidCommand_CreatesDoctorWithNormalizedLicense()
    {
        var result = await HandleAsync(NewCommand(" mp-555 ", 45));

        var doctor = await LoadAsync(result.Id);
        doctor.LicenseNumber.ShouldBe("MP-555");
        doctor.FullName.LastName.ShouldBe("Mora");
        doctor.Email.Value.ShouldBe("luis.mora@dispensario.local");
        doctor.AppointmentDurationMinutes.ShouldBe(45);
        doctor.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WithoutDuration_UsesDefaultOf30()
    {
        var result = await HandleAsync(NewCommand("MP-555", null));

        (await LoadAsync(result.Id)).AppointmentDurationMinutes.ShouldBe(30);
    }

    [Fact]
    public async Task Handle_WithDuplicateLicenseInOtherCase_ThrowsConflictException()
    {
        await using (var context = _db.CreateContext())
        {
            context.Doctors.Add(TestDoctors.Create(licenseNumber: "MP-555"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var exception = await Should.ThrowAsync<ConflictException>(() => HandleAsync(NewCommand("mp-555", null)));

        exception.Message.ShouldBe("Doctor with license number MP-555 already exists.");
    }

    public void Dispose() => _db.Dispose();

    private static CreateDoctorCommand NewCommand(string licenseNumber, int? duration) =>
        new("Luis", "Mora", licenseNumber, "Pediatría", "Luis.Mora@dispensario.local", null, duration);

    private async Task<Doctor> LoadAsync(Guid id)
    {
        var doctorId = new DoctorId(id);
        await using var context = _db.CreateContext();
        return await context.Doctors.SingleAsync(d => d.Id == doctorId, TestContext.Current.CancellationToken);
    }

    private async Task<CreateDoctorResult> HandleAsync(CreateDoctorCommand command)
    {
        await using var context = _db.CreateContext();
        return await new CreateDoctorHandler(context).Handle(command, TestContext.Current.CancellationToken);
    }
}
