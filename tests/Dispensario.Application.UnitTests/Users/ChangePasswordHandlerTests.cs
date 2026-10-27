using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Application.Users.Commands.ChangePassword;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Users;

public sealed class ChangePasswordHandlerTests : IDisposable
{
    private const string CurrentPassword = "OldPassw0rd";
    private const string NewPassword = "NewPassw0rd";
    private static readonly ChangePasswordCommand Command = new(CurrentPassword, NewPassword);

    private readonly DispensarioTestDb _db = new();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();

    public ChangePasswordHandlerTests()
    {
        _hasher.Verify("old-hash", CurrentPassword).Returns(true);
        _hasher.Hash(NewPassword).Returns("new-hash");
    }

    [Fact]
    public async Task Handle_WithCorrectCurrentPassword_SavesNewHash()
    {
        var user = await AddCurrentUserAsync(TestUsers.Create(passwordHash: "old-hash"));

        await HandleAsync(Command);

        (await LoadAsync(user.Id)).PasswordHash.ShouldBe("new-hash");
    }

    [Fact]
    public async Task Handle_WithWrongCurrentPassword_ThrowsBadRequestException()
    {
        var user = await AddCurrentUserAsync(TestUsers.Create(passwordHash: "old-hash"));

        var exception = await Should.ThrowAsync<BadRequestException>(
            () => HandleAsync(new ChangePasswordCommand("WrongPassw0rd", NewPassword)));

        exception.Message.ShouldBe("Current password is incorrect.");
        (await LoadAsync(user.Id)).PasswordHash.ShouldBe("old-hash");
    }

    [Fact]
    public async Task Handle_WithoutUserId_ThrowsUnauthorizedException()
    {
        _db.CurrentUser.UserId.Returns((UserId?)null);

        await Should.ThrowAsync<UnauthorizedException>(() => HandleAsync(Command));
    }

    [Fact]
    public async Task Handle_WithUnknownUser_ThrowsNotFoundException()
    {
        _db.CurrentUser.UserId.Returns(UserId.New());

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Command));
    }

    [Fact]
    public async Task Handle_WithInactiveUser_ThrowsUnauthorizedException()
    {
        var user = TestUsers.Create(passwordHash: "old-hash");
        user.Deactivate();
        await AddCurrentUserAsync(user);

        var exception = await Should.ThrowAsync<UnauthorizedException>(() => HandleAsync(Command));

        exception.Message.ShouldBe("User is inactive.");
    }

    public void Dispose() => _db.Dispose();

    private async Task<User> AddCurrentUserAsync(User user)
    {
        await using var context = _db.CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.CurrentUser.UserId.Returns(user.Id);
        return user;
    }

    private async Task<User> LoadAsync(UserId userId)
    {
        await using var context = _db.CreateContext();
        return await context.Users.SingleAsync(u => u.Id == userId, TestContext.Current.CancellationToken);
    }

    private async Task HandleAsync(ChangePasswordCommand command)
    {
        await using var context = _db.CreateContext();
        await new ChangePasswordHandler(context, _hasher, _db.CurrentUser).Handle(command, TestContext.Current.CancellationToken);
    }
}
