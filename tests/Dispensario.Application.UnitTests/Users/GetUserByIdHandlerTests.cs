using BuildingBlocks.Exceptions;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Application.Users.Dtos;
using Dispensario.Application.Users.Queries.GetUserById;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using Mapster;

namespace Dispensario.Application.UnitTests.Users;

public sealed class GetUserByIdHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    // En la aplicación lo registra AddApplication con el escaneo del ensamblado
    public GetUserByIdHandlerTests() => TypeAdapterConfig.GlobalSettings.Apply(new UsersMappingConfig());

    [Fact]
    public async Task Handle_WithExistingUser_ReturnsDtoWithCreatedAt()
    {
        var user = TestUsers.Create(Role.Receptionist, phone: "+593 99 123 4567");
        await AddUserAsync(user);

        var result = await HandleAsync(user.Id.Value);

        result.ShouldBe(new UserDto(
            user.Id.Value,
            "ana.perez@dispensario.local",
            "Ana",
            "Pérez",
            DocumentType.NationalId,
            "1712345678",
            "+593 99 123 4567",
            Role.Receptionist,
            DoctorId: null,
            IsActive: true,
            DispensarioTestDb.StartTime.UtcDateTime));
        result.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Handle_WithUnknownUser_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Guid.NewGuid()));
    }

    public void Dispose() => _db.Dispose();

    private async Task AddUserAsync(User user)
    {
        await using var context = _db.CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<UserDto> HandleAsync(Guid userId)
    {
        await using var context = _db.CreateContext();
        var handler = new GetUserByIdHandler(context);
        return await handler.Handle(new GetUserByIdQuery(userId), TestContext.Current.CancellationToken);
    }
}
