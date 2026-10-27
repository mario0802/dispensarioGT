using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Application.Users.Commands.DeactivateUser;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Users;

public sealed class DeactivateUserHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    public DeactivateUserHandlerTests() => _db.CurrentUser.UserId.Returns(UserId.New());

    [Fact]
    public async Task Handle_WithOtherActiveUser_DeactivatesUser()
    {
        var user = await AddUserAsync(TestUsers.Create(Role.Receptionist));

        await HandleAsync(user.Id);

        (await LoadAsync(user.Id)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_WithOwnUser_ThrowsBadRequestException()
    {
        var user = await AddUserAsync(TestUsers.Create(Role.Receptionist));
        _db.CurrentUser.UserId.Returns(user.Id);

        await Should.ThrowAsync<BadRequestException>(() => HandleAsync(user.Id));

        (await LoadAsync(user.Id)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WithLastActiveAdmin_ThrowsConflictException()
    {
        var admin = await AddUserAsync(TestUsers.Create(Role.Admin));
        var inactiveAdmin = TestUsers.Create(Role.Admin, "old.admin@dispensario.local", "1700000009");
        inactiveAdmin.Deactivate();
        await AddUserAsync(inactiveAdmin);

        var exception = await Should.ThrowAsync<ConflictException>(() => HandleAsync(admin.Id));

        exception.Message.ShouldBe("Cannot deactivate the last active admin.");
        (await LoadAsync(admin.Id)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WithTwoActiveAdmins_DeactivatesOne()
    {
        var admin = await AddUserAsync(TestUsers.Create(Role.Admin));
        await AddUserAsync(TestUsers.Create(Role.Admin, "other.admin@dispensario.local", "1700000009"));

        await HandleAsync(admin.Id);

        (await LoadAsync(admin.Id)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_WithInactiveUser_ThrowsDomainException()
    {
        var user = TestUsers.Create(Role.Receptionist);
        user.Deactivate();
        await AddUserAsync(user);

        await Should.ThrowAsync<DomainException>(() => HandleAsync(user.Id));
    }

    [Fact]
    public async Task Handle_WithUnknownUser_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(UserId.New()));
    }

    public void Dispose() => _db.Dispose();

    private async Task<User> AddUserAsync(User user)
    {
        await using var context = _db.CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    private async Task<User> LoadAsync(UserId userId)
    {
        await using var context = _db.CreateContext();
        return await context.Users.SingleAsync(u => u.Id == userId, TestContext.Current.CancellationToken);
    }

    private async Task HandleAsync(UserId userId)
    {
        await using var context = _db.CreateContext();
        await new DeactivateUserHandler(context, _db.CurrentUser)
            .Handle(new DeactivateUserCommand(userId.Value), TestContext.Current.CancellationToken);
    }
}
