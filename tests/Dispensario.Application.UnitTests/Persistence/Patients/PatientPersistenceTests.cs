using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Persistence.Patients;

public sealed class PatientPersistenceTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task SaveChangesAsync_WithAllFields_ReadsBackEqualPatient()
    {
        var address = Address.Create("Av. Amazonas 123", "Quito", "Pichincha", "Frente al parque");
        var patient = TestPatients.Create(
            email: "ana@mail.com", phone: "0991234567", address: address,
            bloodType: BloodType.ABNegative, allergies: "Penicillin");
        await AddAsync(patient);

        var loaded = await LoadAsync(patient.Id);

        loaded.Document.ShouldBe(patient.Document);
        loaded.FullName.ShouldBe(patient.FullName);
        loaded.BirthDate.ShouldBe(new DateOnly(1990, 5, 14));
        loaded.Gender.ShouldBe(Gender.Female);
        loaded.Email.ShouldBe(patient.Email);
        loaded.Phone.ShouldBe(patient.Phone);
        loaded.Address.ShouldBe(address);
        loaded.BloodType.ShouldBe(BloodType.ABNegative);
        loaded.Allergies.ShouldBe("Penicillin");
        loaded.IsActive.ShouldBeTrue();
        loaded.CreatedAtUtc.ShouldBe(DispensarioTestDb.StartTime.UtcDateTime);
        loaded.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task SaveChangesAsync_WithoutOptionals_ReadsBackNulls()
    {
        var patient = TestPatients.Create();
        await AddAsync(patient);

        var loaded = await LoadAsync(patient.Id);

        loaded.Email.ShouldBeNull();
        loaded.Phone.ShouldBeNull();
        loaded.Address.ShouldBeNull();
        loaded.BloodType.ShouldBeNull();
        loaded.Allergies.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_ClearingAddress_ReadsBackNullAddress()
    {
        var patient = TestPatients.Create(address: Address.Create("Av. Amazonas 123", "Quito"));
        await AddAsync(patient);

        await using (var context = _db.CreateContext())
        {
            var tracked = await context.Patients.SingleAsync(p => p.Id == patient.Id, TestContext.Current.CancellationToken);
            tracked.Update(tracked.Document, tracked.FullName, tracked.BirthDate, tracked.Gender,
                null, null, null, null, null, TestPatients.Today);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        (await LoadAsync(patient.Id)).Address.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_WithBloodType_StoresEnumNameAsText()
    {
        await AddAsync(TestPatients.Create(bloodType: BloodType.OPositive));

        await using var command = _db.Connection.CreateCommand();
        command.CommandText = "SELECT BloodType FROM Patients";
        var bloodType = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        bloodType.ShouldBe("OPositive");
    }

    [Fact]
    public async Task SaveChangesAsync_WithDuplicateDocument_ThrowsDbUpdateException()
    {
        await AddAsync(TestPatients.Create(documentNumber: "0102030405"));

        var act = () => AddAsync(TestPatients.Create(documentNumber: "0102030405", firstName: "Otra"));

        await act.ShouldThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsync_WithSameNumberAndOtherType_SavesBoth()
    {
        await AddAsync(TestPatients.Create(documentNumber: "AB123", documentType: DocumentType.NationalId));
        await AddAsync(TestPatients.Create(documentNumber: "AB123", documentType: DocumentType.Passport));

        await using var context = _db.CreateContext();
        (await context.Patients.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    [Fact]
    public async Task EnsureCreated_PatientsTable_HasExpectedColumns()
    {
        var columns = new List<string>();
        await using var command = _db.Connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('Patients')";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            columns.Add(reader.GetString(0));

        columns.ShouldBe(
            [
                "Id", "Document_Type", "Document_Number", "FullName_FirstName", "FullName_LastName", "BirthDate",
                "Gender", "Email", "Phone", "Address_Street", "Address_City", "Address_State", "Address_Reference",
                "BloodType", "Allergies", "IsActive", "CreatedAtUtc", "CreatedBy", "LastModifiedAtUtc", "LastModifiedBy"
            ],
            ignoreOrder: true);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Patient> LoadAsync(PatientId patientId)
    {
        await using var context = _db.CreateContext();
        return await context.Patients.SingleAsync(p => p.Id == patientId, TestContext.Current.CancellationToken);
    }

    private async Task AddAsync(Patient patient)
    {
        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
