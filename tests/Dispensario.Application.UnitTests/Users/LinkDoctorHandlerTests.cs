using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Application.Users.Commands.LinkDoctor;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Users;

public sealed class LinkDoctorHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithDoctorUserAndActiveDoctor_LinksUser()
    {
        var doctor = await AddAsync(TestDoctors.Create());
        var user = await AddAsync(TestUsers.Create(Role.Doctor));

        await HandleAsync(user.Id, doctor.Id);

        (await LoadUserAsync(user.Id)).DoctorId.ShouldBe(doctor.Id);
    }

    [Fact]
    public async Task Handle_WithUnknownUser_ThrowsNotFoundException()
    {
        var doctor = await AddAsync(TestDoctors.Create());

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(UserId.New(), doctor.Id));
    }

    [Fact]
    public async Task Handle_WithUnknownDoctor_ThrowsNotFoundException()
    {
        var user = await AddAsync(TestUsers.Create(Role.Doctor));

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(user.Id, DoctorId.New()));
    }

    [Fact]
    public async Task Handle_WithInactiveDoctor_ThrowsBadRequestException()
    {
        var doctor = TestDoctors.Create();
        doctor.Deactivate();
        await AddAsync(doctor);
        var user = await AddAsync(TestUsers.Create(Role.Doctor));

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(user.Id, doctor.Id));

        exception.Message.ShouldBe("Doctor is inactive.");
    }

    [Fact]
    public async Task Handle_WithDoctorLinkedToOtherUser_ThrowsConflictException()
    {
        var doctor = await AddAsync(TestDoctors.Create());
        var other = TestUsers.Create(Role.Doctor, "other@dispensario.local", "1700000009");
        other.LinkDoctor(doctor.Id);
        await AddAsync(other);
        var user = await AddAsync(TestUsers.Create(Role.Doctor));

        var exception = await Should.ThrowAsync<ConflictException>(() => HandleAsync(user.Id, doctor.Id));

        exception.Message.ShouldBe($"Doctor {doctor.Id.Value} is already linked to another user.");
        (await LoadUserAsync(user.Id)).DoctorId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_WithReceptionistUser_ThrowsDomainException()
    {
        var doctor = await AddAsync(TestDoctors.Create());
        var user = await AddAsync(TestUsers.Create(Role.Receptionist));

        await Should.ThrowAsync<DomainException>(() => HandleAsync(user.Id, doctor.Id));
    }

    [Fact]
    public async Task Handle_WithAlreadyLinkedUser_ThrowsDomainException()
    {
        var first = await AddAsync(TestDoctors.Create(licenseNumber: "MP-111"));
        var second = await AddAsync(TestDoctors.Create(licenseNumber: "MP-222"));
        var user = TestUsers.Create(Role.Doctor);
        user.LinkDoctor(first.Id);
        await AddAsync(user);

        await Should.ThrowAsync<DomainException>(() => HandleAsync(user.Id, second.Id));

        (await LoadUserAsync(user.Id)).DoctorId.ShouldBe(first.Id);
    }

    public void Dispose() => _db.Dispose();

    private async Task<TEntity> AddAsync<TEntity>(TEntity entity) where TEntity : class
    {
        await using var context = _db.CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity;
    }

    private async Task<User> LoadUserAsync(UserId userId)
    {
        await using var context = _db.CreateContext();
        return await context.Users.SingleAsync(u => u.Id == userId, TestContext.Current.CancellationToken);
    }

    private async Task HandleAsync(UserId userId, DoctorId doctorId)
    {
        await using var context = _db.CreateContext();
        await new LinkDoctorHandler(context)
            .Handle(new LinkDoctorCommand(userId.Value, doctorId.Value), TestContext.Current.CancellationToken);
    }
}
