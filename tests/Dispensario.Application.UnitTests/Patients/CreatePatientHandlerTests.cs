using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Patients.Commands.CreatePatient;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Patients;

public sealed class CreatePatientHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithFullCommand_CreatesPatientWithTranslatedBloodTypeAndAddress()
    {
        var command = NewCommand() with
        {
            DocumentNumber = "  0102030405 ",
            Email = "Ana@Mail.com",
            Address = new AddressInput("Av. Amazonas 123", "Quito", null, " "),
            BloodType = "AB-",
            Allergies = " Penicillin ",
        };

        var result = await HandleAsync(command);

        var patient = await LoadAsync(result.Id);
        patient.Document.ShouldBe(Document.Create(DocumentType.NationalId, "0102030405"));
        patient.FullName.LastName.ShouldBe("Pérez");
        patient.Email!.Value.ShouldBe("ana@mail.com");
        patient.Address.ShouldBe(Address.Create("Av. Amazonas 123", "Quito"));
        patient.BloodType.ShouldBe(BloodType.ABNegative);
        patient.Allergies.ShouldBe("Penicillin");
        patient.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WithDuplicateDocument_ThrowsConflictException()
    {
        await AddAsync(TestPatients.Create(documentNumber: "0102030405"));

        var exception = await Should.ThrowAsync<ConflictException>(() => HandleAsync(NewCommand()));

        exception.Message.ShouldBe("Patient with document NationalId 0102030405 already exists.");
    }

    [Fact]
    public async Task Handle_WithSameNumberAndOtherType_CreatesPatient()
    {
        await AddAsync(TestPatients.Create(documentNumber: "0102030405", documentType: DocumentType.Passport));

        var result = await HandleAsync(NewCommand());

        (await LoadAsync(result.Id)).Document.Type.ShouldBe(DocumentType.NationalId);
    }

    [Fact]
    public async Task Handle_WithBirthDateAfterToday_ThrowsDomainException()
    {
        var tomorrow = TestPatients.Today.AddDays(1);

        var exception = await Should.ThrowAsync<DomainException>(() => HandleAsync(NewCommand() with { BirthDate = tomorrow }));

        exception.Message.ShouldBe("Birth date cannot be in the future.");
    }

    [Fact]
    public async Task Handle_WithBirthDateToday_CreatesPatient()
    {
        var result = await HandleAsync(NewCommand() with { BirthDate = TestPatients.Today });

        (await LoadAsync(result.Id)).BirthDate.ShouldBe(TestPatients.Today);
    }

    public void Dispose() => _db.Dispose();

    private static CreatePatientCommand NewCommand() => new(
        DocumentType.NationalId, "0102030405", "Ana", "Pérez", new DateOnly(1990, 5, 14), Gender.Female,
        null, null, null, null, null);

    private async Task AddAsync(Patient patient)
    {
        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Patient> LoadAsync(Guid id)
    {
        var patientId = new PatientId(id);
        await using var context = _db.CreateContext();
        return await context.Patients.SingleAsync(p => p.Id == patientId, TestContext.Current.CancellationToken);
    }

    private async Task<CreatePatientResult> HandleAsync(CreatePatientCommand command)
    {
        await using var context = _db.CreateContext();
        return await new CreatePatientHandler(context, _db.TimeProvider).Handle(command, TestContext.Current.CancellationToken);
    }
}
