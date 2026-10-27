using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Doctors.Commands.ActivateDoctor;
using Dispensario.Application.Doctors.Commands.DeactivateDoctor;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Doctors;

public sealed class DeactivateDoctorHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Deactivate_ActiveDoctor_SetsInactive()
    {
        var doctor = await AddAsync(TestDoctors.Create());

        await DeactivateAsync(doctor.Id);

        (await LoadAsync(doctor.Id)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_InactiveDoctor_ThrowsDomainException()
    {
        var doctor = TestDoctors.Create();
        doctor.Deactivate();
        await AddAsync(doctor);

        await Should.ThrowAsync<DomainException>(() => DeactivateAsync(doctor.Id));
    }

    [Fact]
    public async Task Deactivate_UnknownDoctor_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => DeactivateAsync(DoctorId.New()));
    }

    [Fact]
    public async Task Deactivate_DoctorWithLinkedUser_LeavesUserActiveAndLinked()
    {
        var doctor = await AddAsync(TestDoctors.Create());
        var user = TestUsers.Create(Role.Doctor);
        user.LinkDoctor(doctor.Id);
        await AddAsync(user);

        await DeactivateAsync(doctor.Id);

        await using var context = _db.CreateContext();
        var loaded = await context.Users.SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        loaded.IsActive.ShouldBeTrue();
        loaded.DoctorId.ShouldBe(doctor.Id);
    }

    [Fact]
    public async Task Activate_InactiveDoctor_SetsActive()
    {
        var doctor = TestDoctors.Create();
        doctor.Deactivate();
        await AddAsync(doctor);

        await ActivateAsync(doctor.Id);

        (await LoadAsync(doctor.Id)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Activate_ActiveDoctor_ThrowsDomainException()
    {
        var doctor = await AddAsync(TestDoctors.Create());

        await Should.ThrowAsync<DomainException>(() => ActivateAsync(doctor.Id));
    }

    [Fact]
    public async Task Activate_UnknownDoctor_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => ActivateAsync(DoctorId.New()));
    }

    public void Dispose() => _db.Dispose();

    private async Task<TEntity> AddAsync<TEntity>(TEntity entity) where TEntity : class
    {
        await using var context = _db.CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity;
    }

    private async Task<Doctor> LoadAsync(DoctorId doctorId)
    {
        await using var context = _db.CreateContext();
        return await context.Doctors.SingleAsync(d => d.Id == doctorId, TestContext.Current.CancellationToken);
    }

    private async Task DeactivateAsync(DoctorId doctorId)
    {
        await using var context = _db.CreateContext();
        await new DeactivateDoctorHandler(context)
            .Handle(new DeactivateDoctorCommand(doctorId.Value), TestContext.Current.CancellationToken);
    }

    private async Task ActivateAsync(DoctorId doctorId)
    {
        await using var context = _db.CreateContext();
        await new ActivateDoctorHandler(context)
            .Handle(new ActivateDoctorCommand(doctorId.Value), TestContext.Current.CancellationToken);
    }
}
