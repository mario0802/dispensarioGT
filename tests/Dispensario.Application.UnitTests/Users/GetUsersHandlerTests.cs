using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Application.Users.Dtos;
using Dispensario.Application.Users.Queries.GetUsers;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using Mapster;

namespace Dispensario.Application.UnitTests.Users;

public sealed class GetUsersHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    // En la aplicación lo registra AddApplication con el escaneo del ensamblado
    public GetUsersHandlerTests() => TypeAdapterConfig.GlobalSettings.Apply(new UsersMappingConfig());

    [Fact]
    public async Task Handle_WithPageSize_ReturnsPageAndTotalCount()
    {
        await SeedUsersAsync();

        var result = await HandleAsync(new GetUsersQuery(new PaginationRequest(0, 2), null, null, null));

        result.Count.ShouldBe(4);
        result.PageIndex.ShouldBe(0);
        result.PageSize.ShouldBe(2);
        result.Data.Count().ShouldBe(2);
    }

    [Fact]
    public async Task Handle_WithoutFilters_OrdersByLastNameThenFirstName()
    {
        await SeedUsersAsync();

        var result = await HandleAsync(new GetUsersQuery(new PaginationRequest(), null, null, null));

        result.Data.Select(u => $"{u.LastName} {u.FirstName}")
            .ShouldBe(["Andrade Carla", "Mora Beatriz", "Mora Luis", "Zambrano Diego"]);
    }

    [Fact]
    public async Task Handle_WithSecondPage_SkipsFirstPage()
    {
        await SeedUsersAsync();

        var result = await HandleAsync(new GetUsersQuery(new PaginationRequest(1, 3), null, null, null));

        result.Data.Select(u => u.LastName).ShouldBe(["Zambrano"]);
    }

    [Fact]
    public async Task Handle_WithRoleFilter_ReturnsOnlyThatRole()
    {
        await SeedUsersAsync();

        var result = await HandleAsync(new GetUsersQuery(new PaginationRequest(), null, Role.Doctor, null));

        result.Count.ShouldBe(2);
        result.Data.ShouldAllBe(u => u.Role == Role.Doctor);
    }

    [Fact]
    public async Task Handle_WithIsActiveFilter_ReturnsOnlyInactive()
    {
        await SeedUsersAsync();

        var result = await HandleAsync(new GetUsersQuery(new PaginationRequest(), null, null, false));

        result.Data.Select(u => u.LastName).ShouldBe(["Zambrano"]);
    }

    [Theory]
    [InlineData("MORA", 2)]
    [InlineData("carla", 1)]
    [InlineData("DIEGO.Z@", 1)]
    [InlineData("0999", 1)]
    [InlineData("  andrade  ", 1)]
    [InlineData("nobody", 0)]
    public async Task Handle_WithSearch_MatchesNameEmailOrIdentificationIgnoringCase(string search, int expected)
    {
        await SeedUsersAsync();

        var result = await HandleAsync(new GetUsersQuery(new PaginationRequest(), search, null, null));

        result.Count.ShouldBe(expected);
    }

    [Fact]
    public async Task Handle_WithLikeWildcardInSearch_MatchesLiterally()
    {
        await SeedUsersAsync();

        var result = await HandleAsync(new GetUsersQuery(new PaginationRequest(), "%", null, null));

        result.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    [InlineData(-1, 10)]
    public async Task Handle_WithInvalidPagination_ThrowsBadRequestException(int pageIndex, int pageSize)
    {
        await Should.ThrowAsync<BadRequestException>(
            () => HandleAsync(new GetUsersQuery(new PaginationRequest(pageIndex, pageSize), null, null, null)));
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedUsersAsync()
    {
        var inactive = NewUser("Diego", "Zambrano", "diego.z@dispensario.local", "0999999999", Role.Receptionist);
        inactive.Deactivate();

        await using var context = _db.CreateContext();
        context.Users.AddRange(
            NewUser("Luis", "Mora", "luis.mora@dispensario.local", "1700000001", Role.Doctor),
            NewUser("Beatriz", "Mora", "bea@dispensario.local", "1700000002", Role.Doctor),
            NewUser("Carla", "Andrade", "carla@dispensario.local", "1700000003", Role.Admin),
            inactive);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static User NewUser(string firstName, string lastName, string email, string documentNumber, Role role) =>
        User.Create(
            Email.Create(email),
            "hash",
            FullName.Create(firstName, lastName),
            Document.Create(DocumentType.NationalId, documentNumber),
            null,
            role);

    private async Task<PaginatedResult<UserDto>> HandleAsync(GetUsersQuery query)
    {
        await using var context = _db.CreateContext();
        var handler = new GetUsersHandler(context);
        return await handler.Handle(query, TestContext.Current.CancellationToken);
    }
}
