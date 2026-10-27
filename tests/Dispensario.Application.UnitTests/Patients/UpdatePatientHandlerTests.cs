using BuildingBlocks.Exceptions;
using Dispensario.Application.Patients.Commands.CreatePatient;
using Dispensario.Application.Patients.Commands.UpdatePatient;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Patients;

public sealed class UpdatePatientHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithNewData_UpdatesAllFieldsIncludingDocument()
    {
        var patient = await AddAsync(TestPatients.Create(documentNumber: "0102030405"));

        await HandleAsync(new UpdatePatientCommand(
            patient.Id.Value, DocumentType.Passport, "AB123", "Carla", "Andrade", new DateOnly(2001, 1, 2), Gender.Other,
            "carla@mail.com", "0987654321", new AddressInput("Calle 10", "Cuenca", "Azuay", null), "B-", "Latex"));

        var loaded = await LoadAsync(patient.Id);
        loaded.Document.ShouldBe(Document.Create(DocumentType.Passport, "AB123"));
        loaded.FullName.ShouldBe(FullName.Create("Carla", "Andrade"));
        loaded.BirthDate.ShouldBe(new DateOnly(2001, 1, 2));
        loaded.Gender.ShouldBe(Gender.Other);
        loaded.Email!.Value.ShouldBe("carla@mail.com");
        loaded.Phone!.Value.ShouldBe("0987654321");
        loaded.Address.ShouldBe(Address.Create("Calle 10", "Cuenca", "Azuay"));
        loaded.BloodType.ShouldBe(BloodType.BNegative);
        loaded.Allergies.ShouldBe("Latex");
    }

    [Fact]
    public async Task Handle_WithNullOptionals_ClearsThem()
    {
        var patient = await AddAsync(TestPatients.Create(
            email: "ana@mail.com", phone: "0991234567", address: Address.Create("Av. Amazonas 123", "Quito"),
            bloodType: BloodType.OPositive, allergies: "Penicillin"));

        await HandleAsync(CommandFor(patient.Id, "0102030405"));

        var loaded = await LoadAsync(patient.Id);
        loaded.Email.ShouldBeNull();
        loaded.Phone.ShouldBeNull();
        loaded.Address.ShouldBeNull();
        loaded.BloodType.ShouldBeNull();
        loaded.Allergies.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_KeepingOwnDocument_DoesNotConflict()
    {
        var patient = await AddAsync(TestPatients.Create(documentNumber: "0102030405"));

        await HandleAsync(CommandFor(patient.Id, "0102030405", lastName: "Andrade"));

        (await LoadAsync(patient.Id)).FullName.LastName.ShouldBe("Andrade");
    }

    [Fact]
    public async Task Handle_WithDocumentOfOtherPatient_ThrowsConflictException()
    {
        await AddAsync(TestPatients.Create(documentNumber: "1111111111"));
        var patient = await AddAsync(TestPatients.Create(documentNumber: "2222222222"));

        var exception = await Should.ThrowAsync<ConflictException>(() => HandleAsync(CommandFor(patient.Id, "1111111111")));

        exception.Message.ShouldBe("Patient with document NationalId 1111111111 already exists.");
        (await LoadAsync(patient.Id)).Document.Number.ShouldBe("2222222222");
    }

    [Fact]
    public async Task Handle_WithUnknownPatient_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(CommandFor(PatientId.New(), "0102030405")));
    }

    [Fact]
    public async Task Handle_WithInactivePatient_UpdatesData()
    {
        var patient = TestPatients.Create();
        patient.Deactivate();
        await AddAsync(patient);

        await HandleAsync(CommandFor(patient.Id, "0102030405", lastName: "Andrade"));

        var loaded = await LoadAsync(patient.Id);
        loaded.FullName.LastName.ShouldBe("Andrade");
        loaded.IsActive.ShouldBeFalse();
    }

    public void Dispose() => _db.Dispose();

    private static UpdatePatientCommand CommandFor(PatientId id, string documentNumber, string lastName = "Pérez") => new(
        id.Value, DocumentType.NationalId, documentNumber, "Ana", lastName, new DateOnly(1990, 5, 14), Gender.Female,
        null, null, null, null, null);

    private async Task<Patient> AddAsync(Patient patient)
    {
        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return patient;
    }

    private async Task<Patient> LoadAsync(PatientId patientId)
    {
        await using var context = _db.CreateContext();
        return await context.Patients.SingleAsync(p => p.Id == patientId, TestContext.Current.CancellationToken);
    }

    private async Task HandleAsync(UpdatePatientCommand command)
    {
        await using var context = _db.CreateContext();
        await new UpdatePatientHandler(context, _db.TimeProvider).Handle(command, TestContext.Current.CancellationToken);
    }
}
