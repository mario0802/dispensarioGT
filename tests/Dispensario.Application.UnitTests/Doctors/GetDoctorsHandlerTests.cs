using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using Dispensario.Application.Doctors.Dtos;
using Dispensario.Application.Doctors.Queries.GetDoctors;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;

namespace Dispensario.Application.UnitTests.Doctors;

public sealed class GetDoctorsHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithPageSize_ReturnsPageAndTotalCount()
    {
        await SeedDoctorsAsync();

        var result = await HandleAsync(new GetDoctorsQuery(new PaginationRequest(0, 2), null, null, null));

        result.Count.ShouldBe(4);
        result.PageIndex.ShouldBe(0);
        result.PageSize.ShouldBe(2);
        result.Data.Count().ShouldBe(2);
    }

    [Fact]
    public async Task Handle_WithoutFilters_OrdersByLastNameThenFirstName()
    {
        await SeedDoctorsAsync();

        var result = await HandleAsync(new GetDoctorsQuery(new PaginationRequest(), null, null, null));

        result.Data.Select(d => $"{d.LastName} {d.FirstName}")
            .ShouldBe(["Andrade Carla", "Mora Beatriz", "Mora Luis", "Zambrano Diego"]);
    }

    [Fact]
    public async Task Handle_WithIsActiveFilter_ReturnsOnlyInactive()
    {
        await SeedDoctorsAsync();

        var result = await HandleAsync(new GetDoctorsQuery(new PaginationRequest(), null, null, false));

        result.Data.Select(d => d.LastName).ShouldBe(["Zambrano"]);
    }

    [Theory]
    [InlineData("PEDIATRIA", 2)]
    [InlineData("  cardiologia ", 1)]
    [InlineData("Pedia", 0)]
    public async Task Handle_WithSpecialtyFilter_MatchesWholeValueIgnoringCase(string specialty, int expected)
    {
        await SeedDoctorsAsync();

        var result = await HandleAsync(new GetDoctorsQuery(new PaginationRequest(), null, specialty, null));

        result.Count.ShouldBe(expected);
    }

    [Theory]
    [InlineData("MORA", 2)]
    [InlineData("carla", 1)]
    [InlineData("mp-00", 4)]
    [InlineData("mp-003", 1)]
    [InlineData("DERMA", 1)]
    [InlineData("nobody", 0)]
    public async Task Handle_WithSearch_MatchesNameLicenseOrSpecialtyIgnoringCase(string search, int expected)
    {
        await SeedDoctorsAsync();

        var result = await HandleAsync(new GetDoctorsQuery(new PaginationRequest(), search, null, null));

        result.Count.ShouldBe(expected);
    }

    [Fact]
    public async Task Handle_WithLikeWildcardInSearch_MatchesLiterally()
    {
        await SeedDoctorsAsync();

        var result = await HandleAsync(new GetDoctorsQuery(new PaginationRequest(), "%", null, null));

        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_WithLinkedUser_ReturnsUserIdOrNull()
    {
        var linked = TestDoctors.Create(licenseNumber: "MP-001", lastName: "Andrade");
        var unlinked = TestDoctors.Create(licenseNumber: "MP-002", lastName: "Mora");
        var user = TestUsers.Create(Role.Doctor);
        user.LinkDoctor(linked.Id);
        await using (var context = _db.CreateContext())
        {
            context.AddRange(linked, unlinked, user);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await HandleAsync(new GetDoctorsQuery(new PaginationRequest(), null, null, null));

        result.Data.Select(d => d.UserId).ShouldBe([user.Id.Value, null]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    [InlineData(-1, 10)]
    public async Task Handle_WithInvalidPagination_ThrowsBadRequestException(int pageIndex, int pageSize)
    {
        await Should.ThrowAsync<BadRequestException>(
            () => HandleAsync(new GetDoctorsQuery(new PaginationRequest(pageIndex, pageSize), null, null, null)));
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedDoctorsAsync()
    {
        var inactive = TestDoctors.Create("MP-004", "Diego", "Zambrano", "Dermatología");
        inactive.Deactivate();

        await using var context = _db.CreateContext();
        context.Doctors.AddRange(
            TestDoctors.Create("MP-001", "Luis", "Mora", "Pediatria"),
            TestDoctors.Create("MP-002", "Beatriz", "Mora", "Pediatria"),
            TestDoctors.Create("MP-003", "Carla", "Andrade", "Cardiologia"),
            inactive);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<PaginatedResult<DoctorDto>> HandleAsync(GetDoctorsQuery query)
    {
        await using var context = _db.CreateContext();
        return await new GetDoctorsHandler(context).Handle(query, TestContext.Current.CancellationToken);
    }
}
