using BuildingBlocks.Exceptions;
using Dispensario.Application.Abstractions;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Application.Users.Commands.Login;
using Dispensario.Domain.Users;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Users;

public sealed class LoginHandlerTests : IDisposable
{
    private const string Password = "S3cret-Passw0rd!";
    private const string PasswordHash = "stored-hash";

    private static readonly DateTime ExpiresAtUtc = new(2026, 10, 26, 13, 0, 0, DateTimeKind.Utc);

    private readonly DispensarioTestDb _db = new();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _tokenGenerator = Substitute.For<IJwtTokenGenerator>();

    public LoginHandlerTests()
    {
        _hasher.Verify(PasswordHash, Password).Returns(true);
        _tokenGenerator.Generate(Arg.Any<User>()).Returns(("token", ExpiresAtUtc));
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsBearerToken()
    {
        var user = await AddUserAsync(TestUsers.Create(passwordHash: PasswordHash));

        var result = await HandleAsync(new LoginCommand("ana.perez@dispensario.local", Password));

        result.ShouldBe(new LoginResult("token", "Bearer", ExpiresAtUtc));
        _tokenGenerator.Received(1).Generate(Arg.Is<User>(u => u.Id == user.Id));
    }

    [Fact]
    public async Task Handle_WithUppercaseEmail_FindsUser()
    {
        await AddUserAsync(TestUsers.Create(passwordHash: PasswordHash));

        var result = await HandleAsync(new LoginCommand("ANA.PEREZ@Dispensario.Local", Password));

        result.AccessToken.ShouldBe("token");
    }

    [Fact]
    public async Task Handle_WithUnknownEmail_ThrowsUnauthorizedException()
    {
        await AddUserAsync(TestUsers.Create(passwordHash: PasswordHash));

        var exception = await Should.ThrowAsync<UnauthorizedException>(
            () => HandleAsync(new LoginCommand("someone.else@dispensario.local", Password)));

        exception.Message.ShouldBe("Invalid credentials.");
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ThrowsUnauthorizedException()
    {
        await AddUserAsync(TestUsers.Create(passwordHash: PasswordHash));

        var exception = await Should.ThrowAsync<UnauthorizedException>(
            () => HandleAsync(new LoginCommand("ana.perez@dispensario.local", "wrong-password")));

        exception.Message.ShouldBe("Invalid credentials.");
    }

    [Fact]
    public async Task Handle_WithInactiveUser_ThrowsUnauthorizedException()
    {
        var user = TestUsers.Create(passwordHash: PasswordHash);
        user.Deactivate();
        await AddUserAsync(user);

        var exception = await Should.ThrowAsync<UnauthorizedException>(
            () => HandleAsync(new LoginCommand("ana.perez@dispensario.local", Password)));

        exception.Message.ShouldBe("Invalid credentials.");
        _tokenGenerator.DidNotReceive().Generate(Arg.Any<User>());
    }

    public void Dispose() => _db.Dispose();

    private async Task<User> AddUserAsync(User user)
    {
        await using var context = _db.CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    private async Task<LoginResult> HandleAsync(LoginCommand command)
    {
        await using var context = _db.CreateContext();
        var handler = new LoginHandler(context, _hasher, _tokenGenerator);
        return await handler.Handle(command, TestContext.Current.CancellationToken);
    }
}
