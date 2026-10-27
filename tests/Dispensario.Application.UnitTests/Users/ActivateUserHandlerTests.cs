using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Application.Users.Commands.ActivateUser;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Users;

public sealed class ActivateUserHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithInactiveUser_ActivatesUser()
    {
        var user = TestUsers.Create(Role.Receptionist);
        user.Deactivate();
        await AddUserAsync(user);

        await HandleAsync(user.Id);

        (await LoadAsync(user.Id)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WithActiveUser_ThrowsDomainException()
    {
        var user = await AddUserAsync(TestUsers.Create(Role.Receptionist));

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
        await new ActivateUserHandler(context).Handle(new ActivateUserCommand(userId.Value), TestContext.Current.CancellationToken);
    }
}
