using BuildingBlocks.Exceptions;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Application.Users.Commands.UpdateCurrentUserProfile;
using Dispensario.Application.Users.Commands.UpdateUserProfile;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Users;

// Cubre UpdateUserProfileHandler (Admin) y UpdateCurrentUserProfileHandler (usuario actual)
public sealed class UpdateUserProfileHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithExistingUser_UpdatesNameAndPhone()
    {
        var user = await AddUserAsync(TestUsers.Create());

        await HandleAsync(new UpdateUserProfileCommand(user.Id.Value, "Ana María", "Pérez Ruiz", "0991234567"));

        var saved = await LoadAsync(user.Id);
        saved.FullName.FirstName.ShouldBe("Ana María");
        saved.FullName.LastName.ShouldBe("Pérez Ruiz");
        saved.Phone!.Value.ShouldBe("0991234567");
    }

    [Fact]
    public async Task Handle_WithNullPhone_ClearsPhone()
    {
        var user = await AddUserAsync(TestUsers.Create(phone: "0991234567"));

        await HandleAsync(new UpdateUserProfileCommand(user.Id.Value, "Ana", "Pérez", null));

        (await LoadAsync(user.Id)).Phone.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_WithUnknownUser_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(
            () => HandleAsync(new UpdateUserProfileCommand(Guid.NewGuid(), "Ana", "Pérez", null)));
    }

    [Fact]
    public async Task HandleCurrent_WithActiveUser_UpdatesCurrentUser()
    {
        var user = await AddUserAsync(TestUsers.Create());
        _db.CurrentUser.UserId.Returns(user.Id);

        await HandleCurrentAsync(new UpdateCurrentUserProfileCommand("Anita", "Pérez", "0991234567"));

        var saved = await LoadAsync(user.Id);
        saved.FullName.FirstName.ShouldBe("Anita");
        saved.Phone!.Value.ShouldBe("0991234567");
    }

    [Fact]
    public async Task HandleCurrent_WithoutUserId_ThrowsUnauthorizedException()
    {
        _db.CurrentUser.UserId.Returns((UserId?)null);

        await Should.ThrowAsync<UnauthorizedException>(
            () => HandleCurrentAsync(new UpdateCurrentUserProfileCommand("Ana", "Pérez", null)));
    }

    [Fact]
    public async Task HandleCurrent_WithUnknownUser_ThrowsNotFoundException()
    {
        _db.CurrentUser.UserId.Returns(UserId.New());

        await Should.ThrowAsync<NotFoundException>(
            () => HandleCurrentAsync(new UpdateCurrentUserProfileCommand("Ana", "Pérez", null)));
    }

    [Fact]
    public async Task HandleCurrent_WithInactiveUser_ThrowsUnauthorizedException()
    {
        var user = TestUsers.Create();
        user.Deactivate();
        await AddUserAsync(user);
        _db.CurrentUser.UserId.Returns(user.Id);

        var exception = await Should.ThrowAsync<UnauthorizedException>(
            () => HandleCurrentAsync(new UpdateCurrentUserProfileCommand("Ana", "Pérez", null)));

        exception.Message.ShouldBe("User is inactive.");
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

    private async Task HandleAsync(UpdateUserProfileCommand command)
    {
        await using var context = _db.CreateContext();
        await new UpdateUserProfileHandler(context).Handle(command, TestContext.Current.CancellationToken);
    }

    private async Task HandleCurrentAsync(UpdateCurrentUserProfileCommand command)
    {
        await using var context = _db.CreateContext();
        await new UpdateCurrentUserProfileHandler(context, _db.CurrentUser).Handle(command, TestContext.Current.CancellationToken);
    }
}
