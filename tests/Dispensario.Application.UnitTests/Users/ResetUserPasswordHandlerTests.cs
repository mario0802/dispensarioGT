using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Application.Users.Commands.ResetUserPassword;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Users;

public sealed class ResetUserPasswordHandlerTests : IDisposable
{
    private const string NewPassword = "NewPassw0rd";

    private readonly DispensarioTestDb _db = new();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();

    public ResetUserPasswordHandlerTests()
    {
        _hasher.Hash(NewPassword).Returns("new-hash");
        _db.CurrentUser.UserId.Returns(UserId.New());
    }

    [Fact]
    public async Task Handle_WithOtherUser_SavesNewHash()
    {
        var user = await AddUserAsync(TestUsers.Create(Role.Receptionist, passwordHash: "old-hash"));

        await HandleAsync(new ResetUserPasswordCommand(user.Id.Value, NewPassword));

        (await LoadAsync(user.Id)).PasswordHash.ShouldBe("new-hash");
    }

    [Fact]
    public async Task Handle_WithInactiveUser_SavesNewHash()
    {
        var user = TestUsers.Create(Role.Receptionist, passwordHash: "old-hash");
        user.Deactivate();
        await AddUserAsync(user);

        await HandleAsync(new ResetUserPasswordCommand(user.Id.Value, NewPassword));

        (await LoadAsync(user.Id)).PasswordHash.ShouldBe("new-hash");
    }

    [Fact]
    public async Task Handle_WithOwnUser_ThrowsBadRequestException()
    {
        var user = await AddUserAsync(TestUsers.Create(passwordHash: "old-hash"));
        _db.CurrentUser.UserId.Returns(user.Id);

        await Should.ThrowAsync<BadRequestException>(() => HandleAsync(new ResetUserPasswordCommand(user.Id.Value, NewPassword)));

        (await LoadAsync(user.Id)).PasswordHash.ShouldBe("old-hash");
    }

    [Fact]
    public async Task Handle_WithUnknownUser_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(new ResetUserPasswordCommand(Guid.NewGuid(), NewPassword)));
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

    private async Task HandleAsync(ResetUserPasswordCommand command)
    {
        await using var context = _db.CreateContext();
        await new ResetUserPasswordHandler(context, _hasher, _db.CurrentUser).Handle(command, TestContext.Current.CancellationToken);
    }
}
