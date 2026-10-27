using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using Dispensario.Application.Patients.Dtos;
using Dispensario.Application.Patients.Queries.GetPatients;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;
using Mapster;

namespace Dispensario.Application.UnitTests.Patients;

public sealed class GetPatientsHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    public GetPatientsHandlerTests() => TypeAdapterConfig.GlobalSettings.Apply(new PatientsMappingConfig());

    [Fact]
    public async Task Handle_WithPageSize_ReturnsPageAndTotalCount()
    {
        await SeedPatientsAsync();

        var result = await HandleAsync(new GetPatientsQuery(new PaginationRequest(0, 2), null, null));

        result.Count.ShouldBe(4);
        result.PageIndex.ShouldBe(0);
        result.PageSize.ShouldBe(2);
        result.Data.Count().ShouldBe(2);
    }

    [Fact]
    public async Task Handle_WithoutFilters_OrdersByLastNameThenFirstName()
    {
        await SeedPatientsAsync();

        var result = await HandleAsync(new GetPatientsQuery(new PaginationRequest(), null, null));

        result.Data.Select(p => $"{p.LastName} {p.FirstName}")
            .ShouldBe(["Andrade Carla", "Mora Beatriz", "Mora Luis", "Zambrano Diego"]);
    }

    [Fact]
    public async Task Handle_WithIsActiveFilter_ReturnsOnlyInactive()
    {
        await SeedPatientsAsync();

        var result = await HandleAsync(new GetPatientsQuery(new PaginationRequest(), null, false));

        result.Data.Select(p => p.LastName).ShouldBe(["Zambrano"]);
    }

    [Theory]
    [InlineData("MORA", 2)]
    [InlineData(" carla ", 1)]
    [InlineData("1700", 4)]
    [InlineData("1700000003", 1)]
    [InlineData("nobody", 0)]
    public async Task Handle_WithSearch_MatchesNameOrDocumentNumberIgnoringCase(string search, int expected)
    {
        await SeedPatientsAsync();

        var result = await HandleAsync(new GetPatientsQuery(new PaginationRequest(), search, null));

        result.Count.ShouldBe(expected);
    }

    [Fact]
    public async Task Handle_WithSearchInOtherCase_MatchesAlphanumericDocument()
    {
        await AddAsync(TestPatients.Create(documentNumber: "AB-9", documentType: DocumentType.Passport, lastName: "Paz"));

        var result = await HandleAsync(new GetPatientsQuery(new PaginationRequest(), "ab-9", null));

        result.Data.Select(p => p.LastName).ShouldBe(["Paz"]);
    }

    [Fact]
    public async Task Handle_WithLikeWildcardInSearch_MatchesLiterally()
    {
        await AddAsync(TestPatients.Create(documentNumber: "50%OFF", lastName: "Literal"));
        await AddAsync(TestPatients.Create(documentNumber: "50XOFF", lastName: "Other"));

        var result = await HandleAsync(new GetPatientsQuery(new PaginationRequest(), "50%", null));

        result.Data.Select(p => p.LastName).ShouldBe(["Literal"]);
    }

    [Fact]
    public async Task Handle_WithBloodType_ReturnsClinicalNotation()
    {
        await AddAsync(TestPatients.Create(bloodType: BloodType.OPositive));

        var result = await HandleAsync(new GetPatientsQuery(new PaginationRequest(), null, null));

        result.Data.Single().BloodType.ShouldBe("O+");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Handle_WithPageSizeOutOfRange_ThrowsBadRequestException(int pageSize)
    {
        await Should.ThrowAsync<BadRequestException>(() =>
            HandleAsync(new GetPatientsQuery(new PaginationRequest(0, pageSize), null, null)));
    }

    [Fact]
    public async Task Handle_WithNegativePageIndex_ThrowsBadRequestException()
    {
        await Should.ThrowAsync<BadRequestException>(() =>
            HandleAsync(new GetPatientsQuery(new PaginationRequest(-1, 10), null, null)));
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedPatientsAsync()
    {
        await AddAsync(TestPatients.Create(documentNumber: "1700000001", firstName: "Luis", lastName: "Mora"));
        await AddAsync(TestPatients.Create(documentNumber: "1700000002", firstName: "Beatriz", lastName: "Mora"));
        await AddAsync(TestPatients.Create(documentNumber: "1700000003", firstName: "Carla", lastName: "Andrade"));
        var inactive = TestPatients.Create(documentNumber: "1700000004", firstName: "Diego", lastName: "Zambrano");
        inactive.Deactivate();
        await AddAsync(inactive);
    }

    private async Task AddAsync(Patient patient)
    {
        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<PaginatedResult<PatientDto>> HandleAsync(GetPatientsQuery query)
    {
        await using var context = _db.CreateContext();
        return await new GetPatientsHandler(context).Handle(query, TestContext.Current.CancellationToken);
    }
}
