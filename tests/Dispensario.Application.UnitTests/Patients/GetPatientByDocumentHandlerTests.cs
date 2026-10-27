using BuildingBlocks.Exceptions;
using Dispensario.Application.Patients.Dtos;
using Dispensario.Application.Patients.Queries.GetPatientByDocument;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;
using Mapster;

namespace Dispensario.Application.UnitTests.Patients;

public sealed class GetPatientByDocumentHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    public GetPatientByDocumentHandlerTests() => TypeAdapterConfig.GlobalSettings.Apply(new PatientsMappingConfig());

    [Fact]
    public async Task Handle_WithNumberSurroundedBySpaces_ReturnsPatient()
    {
        var patient = await AddAsync(TestPatients.Create(documentNumber: "0102030405"));

        var dto = await HandleAsync(DocumentType.NationalId, "  0102030405 ");

        dto.Id.ShouldBe(patient.Id.Value);
    }

    [Fact]
    public async Task Handle_WithSameNumberAndOtherType_ThrowsNotFoundException()
    {
        await AddAsync(TestPatients.Create(documentNumber: "0102030405"));

        var exception = await Should.ThrowAsync<NotFoundException>(() => HandleAsync(DocumentType.Passport, "0102030405"));

        exception.Message.ShouldBe("Patient with document Passport 0102030405 was not found.");
    }

    [Fact]
    public async Task Handle_WithOtherCase_ThrowsNotFoundException()
    {
        await AddAsync(TestPatients.Create(documentNumber: "AB123", documentType: DocumentType.Passport));

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(DocumentType.Passport, "ab123"));
    }

    [Fact]
    public async Task Handle_WithInactivePatient_ReturnsPatient()
    {
        var patient = TestPatients.Create();
        patient.Deactivate();
        await AddAsync(patient);

        var dto = await HandleAsync(DocumentType.NationalId, "0102030405");

        dto.IsActive.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Handle_WithBlankNumber_ThrowsBadRequestException(string? number)
    {
        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(DocumentType.NationalId, number!));

        exception.Message.ShouldBe("Document number is required.");
    }

    public void Dispose() => _db.Dispose();

    private async Task<Patient> AddAsync(Patient patient)
    {
        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return patient;
    }

    private async Task<PatientDto> HandleAsync(DocumentType type, string number)
    {
        await using var context = _db.CreateContext();
        return await new GetPatientByDocumentHandler(context)
            .Handle(new GetPatientByDocumentQuery(type, number), TestContext.Current.CancellationToken);
    }
}
