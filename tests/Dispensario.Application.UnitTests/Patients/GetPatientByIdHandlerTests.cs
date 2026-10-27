using BuildingBlocks.Exceptions;
using Dispensario.Application.Patients.Dtos;
using Dispensario.Application.Patients.Queries.GetPatientById;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;
using Mapster;

namespace Dispensario.Application.UnitTests.Patients;

public sealed class GetPatientByIdHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    public GetPatientByIdHandlerTests() => TypeAdapterConfig.GlobalSettings.Apply(new PatientsMappingConfig());

    [Fact]
    public async Task Handle_WithAllFields_ReturnsFullDto()
    {
        var patient = TestPatients.Create(
            email: "ana@mail.com", phone: "0991234567",
            address: Address.Create("Av. Amazonas 123", "Quito", "Pichincha", "Frente al parque"),
            bloodType: BloodType.ABNegative, allergies: "Penicillin");
        await AddAsync(patient);

        var dto = await HandleAsync(patient.Id.Value);

        dto.ShouldBe(new PatientDto(
            patient.Id.Value,
            DocumentType.NationalId,
            "0102030405",
            "Ana",
            "Pérez",
            new DateOnly(1990, 5, 14),
            Gender.Female,
            "ana@mail.com",
            "0991234567",
            new AddressDto("Av. Amazonas 123", "Quito", "Pichincha", "Frente al parque"),
            "AB-",
            "Penicillin",
            true,
            DispensarioTestDb.StartTime.UtcDateTime));
    }

    [Fact]
    public async Task Handle_WithoutOptionals_ReturnsNulls()
    {
        var patient = TestPatients.Create();
        await AddAsync(patient);

        var dto = await HandleAsync(patient.Id.Value);

        dto.Address.ShouldBeNull();
        dto.BloodType.ShouldBeNull();
        dto.Email.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_WithUnknownPatient_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(PatientId.New().Value));
    }

    public void Dispose() => _db.Dispose();

    private async Task AddAsync(Patient patient)
    {
        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<PatientDto> HandleAsync(Guid id)
    {
        await using var context = _db.CreateContext();
        return await new GetPatientByIdHandler(context).Handle(new GetPatientByIdQuery(id), TestContext.Current.CancellationToken);
    }
}
