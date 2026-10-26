using BuildingBlocks.Exceptions;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Application.Users.Dtos;
using Dispensario.Application.Users.Queries.GetCurrentUser;
using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Mapster;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Users;

public sealed class GetCurrentUserHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    // En la aplicación lo registra AddApplication con el escaneo del ensamblado
    public GetCurrentUserHandlerTests() => TypeAdapterConfig.GlobalSettings.Apply(new UsersMappingConfig());

    [Fact]
    public async Task Handle_WithActiveUser_ReturnsAllFields()
    {
        var user = TestUsers.Create(Role.Doctor, phone: "+593 99 123 4567");
        var doctorId = DoctorId.New();
        user.LinkDoctor(doctorId);
        await AddUserAsync(user);
        _db.CurrentUser.UserId.Returns(user.Id);

        var result = await HandleAsync();

        result.ShouldBe(new CurrentUserDto(
            user.Id.Value,
            "ana.perez@dispensario.local",
            "Ana",
            "Pérez",
            DocumentType.NationalId,
            "1712345678",
            user.Phone!.Value,
            Role.Doctor,
            doctorId.Value,
            IsActive: true));
    }

    [Fact]
    public async Task Handle_WithoutPhoneAndDoctor_ReturnsNulls()
    {
        var user = await AddUserAsync(TestUsers.Create());
        _db.CurrentUser.UserId.Returns(user.Id);

        var result = await HandleAsync();

        result.Phone.ShouldBeNull();
        result.DoctorId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_WithoutUserId_ThrowsUnauthorizedException()
    {
        _db.CurrentUser.UserId.Returns((UserId?)null);

        var exception = await Should.ThrowAsync<UnauthorizedException>(HandleAsync);

        exception.Message.ShouldBe("User is not authenticated.");
    }

    [Fact]
    public async Task Handle_WithUnknownUser_ThrowsNotFoundException()
    {
        _db.CurrentUser.UserId.Returns(UserId.New());

        await Should.ThrowAsync<NotFoundException>(HandleAsync);
    }

    [Fact]
    public async Task Handle_WithInactiveUser_ThrowsUnauthorizedException()
    {
        var user = TestUsers.Create();
        user.Deactivate();
        await AddUserAsync(user);
        _db.CurrentUser.UserId.Returns(user.Id);

        var exception = await Should.ThrowAsync<UnauthorizedException>(HandleAsync);

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

    private async Task<CurrentUserDto> HandleAsync()
    {
        await using var context = _db.CreateContext();
        var handler = new GetCurrentUserHandler(context, _db.CurrentUser);
        return await handler.Handle(new GetCurrentUserQuery(), TestContext.Current.CancellationToken);
    }
}
