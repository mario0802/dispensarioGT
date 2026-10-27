using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Application.Users.Commands.RegisterUser;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Users;

public sealed class RegisterUserHandlerTests : IDisposable
{
    private const string Password = "Passw0rd";
    private const string PasswordHash = "hashed-password";

    private static readonly RegisterUserCommand Command = new(
        "Luis.Mora@Dispensario.Local",
        Password,
        "Luis",
        "Mora",
        DocumentType.Passport,
        "AB123456",
        null,
        Role.Doctor);

    private readonly DispensarioTestDb _db = new();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();

    public RegisterUserHandlerTests() => _hasher.Hash(Password).Returns(PasswordHash);

    [Fact]
    public async Task Handle_WithNewUser_SavesUserWithHashRoleAndLowercaseEmail()
    {
        var result = await HandleAsync(Command);

        await using var context = _db.CreateContext();
        var user = await context.Users.SingleAsync(u => u.Id == new UserId(result.Id), TestContext.Current.CancellationToken);
        user.Email.Value.ShouldBe("luis.mora@dispensario.local");
        user.PasswordHash.ShouldBe(PasswordHash);
        user.Role.ShouldBe(Role.Doctor);
        user.DoctorId.ShouldBeNull();
        user.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WithDuplicateEmailInOtherCase_ThrowsConflictException()
    {
        await AddUserAsync(TestUsers.Create(email: "luis.mora@dispensario.local"));

        await Should.ThrowAsync<ConflictException>(() => HandleAsync(Command));
    }

    [Fact]
    public async Task Handle_WithDuplicateIdentification_ThrowsConflictException()
    {
        await AddUserAsync(User.Create(
            Email.Create("other@dispensario.local"),
            "hash",
            FullName.Create("Otro", "Usuario"),
            Document.Create(DocumentType.Passport, "AB123456"),
            null,
            Role.Receptionist));

        await Should.ThrowAsync<ConflictException>(() => HandleAsync(Command));
    }

    public void Dispose() => _db.Dispose();

    private async Task AddUserAsync(User user)
    {
        await using var context = _db.CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<RegisterUserResult> HandleAsync(RegisterUserCommand command)
    {
        await using var context = _db.CreateContext();
        var handler = new RegisterUserHandler(context, _hasher);
        return await handler.Handle(command, TestContext.Current.CancellationToken);
    }
}
