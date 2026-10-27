using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Dispensario.Domain.Users;
using Dispensario.Infrastructure.Authentication;
using Dispensario.Infrastructure.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dispensario.Application.UnitTests.Persistence.Seed;

public sealed class SampleDataSeederTests : IDisposable
{
    private const string Password = "Sample-Seed-Password-1";

    private readonly DispensarioTestDb _db = new();
    private readonly BcryptPasswordHasher _hasher = new();
    private readonly ListLogger<SampleDataSeeder> _logger = new();

    [Fact]
    public async Task SeedAsync_OnEmptyDatabase_CreatesDoctorsWithSchedules()
    {
        await SeedAsync(CreateOptions(string.Empty));

        await using var context = _db.CreateContext();
        var doctors = await context.Doctors.ToListAsync(TestContext.Current.CancellationToken);
        doctors.Select(d => (d.LicenseNumber, d.AppointmentDurationMinutes, d.Schedule.Count, d.IsActive))
            .ShouldBe(
                [
                    ("MG-0001", 30, 10, true),
                    ("PED-0002", 20, 5, true),
                    ("CAR-0003", 40, 2, true)
                ],
                ignoreOrder: true);
    }

    [Fact]
    public async Task SeedAsync_OnEmptyDatabase_CreatesPatients()
    {
        await SeedAsync(CreateOptions(string.Empty));

        await using var context = _db.CreateContext();
        var patients = await context.Patients.ToListAsync(TestContext.Current.CancellationToken);
        patients.Select(p => p.Document)
            .ShouldBe(
                [
                    Document.Create(DocumentType.NationalId, "1710000001"),
                    Document.Create(DocumentType.NationalId, "1710000002"),
                    Document.Create(DocumentType.NationalId, "0910000003"),
                    Document.Create(DocumentType.NationalId, "0910000004"),
                    Document.Create(DocumentType.NationalId, "1310000005")
                ],
                ignoreOrder: true);
        patients.ShouldAllBe(p => p.IsActive);
    }

    [Fact]
    public async Task SeedAsync_WithPassword_CreatesUsersAndLinksDoctor()
    {
        await SeedAsync(CreateOptions(Password));

        await using var context = _db.CreateContext();
        var generalDoctor = await context.Doctors.SingleAsync(d => d.LicenseNumber == "MG-0001", TestContext.Current.CancellationToken);
        var users = await context.Users.ToListAsync(TestContext.Current.CancellationToken);
        users.Count.ShouldBe(2);

        var receptionist = users.Single(u => u.Email == Email.Create("recepcion@dispensario.local"));
        receptionist.Role.ShouldBe(Role.Receptionist);
        receptionist.DoctorId.ShouldBeNull();
        receptionist.IsActive.ShouldBeTrue();
        _hasher.Verify(receptionist.PasswordHash, Password).ShouldBeTrue();

        var doctor = users.Single(u => u.Email == Email.Create("doctor@dispensario.local"));
        doctor.Role.ShouldBe(Role.Doctor);
        doctor.DoctorId.ShouldBe(generalDoctor.Id);
        doctor.IsActive.ShouldBeTrue();
        _hasher.Verify(doctor.PasswordHash, Password).ShouldBeTrue();
    }

    [Fact]
    public async Task SeedAsync_WithEmptyPassword_SkipsUsersAndLogsWarning()
    {
        await SeedAsync(CreateOptions(string.Empty));

        await using var context = _db.CreateContext();
        (await context.Doctors.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(3);
        (await context.Patients.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(5);
        (await context.Users.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        _logger.Entries.ShouldContain(entry =>
            entry.Level == LogLevel.Warning
            && entry.Message == "Sample users skipped: 'Seed:SampleData:Password' is empty.");
    }

    [Fact]
    public async Task SeedAsync_WhenDisabled_DoesNothing()
    {
        await SeedAsync(CreateOptions(Password, enabled: false));

        await using var context = _db.CreateContext();
        (await context.Doctors.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await context.Patients.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await context.Users.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task SeedAsync_WhenDoctorsExist_DoesNothing()
    {
        await AddAsync(Doctor.Create(
            FullName.Create("Existing", "Doctor"),
            "EX-0001",
            "Dermatología",
            Email.Create("existing.doctor@mail.com"),
            phone: null,
            appointmentDurationMinutes: 30));

        await SeedAsync(CreateOptions(Password));

        await using var context = _db.CreateContext();
        (await context.Doctors.SingleAsync(TestContext.Current.CancellationToken)).LicenseNumber.ShouldBe("EX-0001");
        (await context.Patients.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await context.Users.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task SeedAsync_WhenPatientsExist_DoesNothing()
    {
        await AddAsync(Patient.Create(
            Document.Create(DocumentType.Passport, "EX123456"),
            FullName.Create("Existing", "Patient"),
            new DateOnly(1990, 1, 1),
            Gender.Other,
            email: null,
            phone: null,
            address: null,
            bloodType: null,
            allergies: null,
            DateOnly.FromDateTime(DispensarioTestDb.StartTime.UtcDateTime)));

        await SeedAsync(CreateOptions(Password));

        await using var context = _db.CreateContext();
        (await context.Patients.SingleAsync(TestContext.Current.CancellationToken)).Document
            .ShouldBe(Document.Create(DocumentType.Passport, "EX123456"));
        (await context.Doctors.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await context.Users.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicate()
    {
        await SeedAsync(CreateOptions(Password));

        await SeedAsync(CreateOptions(Password));

        await using var context = _db.CreateContext();
        (await context.Doctors.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(3);
        (await context.Patients.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(5);
        (await context.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    [Fact]
    public async Task SeedAsync_WhenUserEmailExists_SkipsThatUser()
    {
        var existing = User.Create(
            Email.Create("recepcion@dispensario.local"),
            "$2a$11$hash",
            FullName.Create("Existing", "Receptionist"),
            Document.Create(DocumentType.Passport, "EX654321"),
            phone: null,
            Role.Receptionist);
        await AddAsync(existing);

        await SeedAsync(CreateOptions(Password));

        await using var context = _db.CreateContext();
        var users = await context.Users.ToListAsync(TestContext.Current.CancellationToken);
        users.Count.ShouldBe(2);
        users.Single(u => u.Email == Email.Create("recepcion@dispensario.local")).Id.ShouldBe(existing.Id);
        users.ShouldContain(u => u.Email == Email.Create("doctor@dispensario.local"));
        (await context.Doctors.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(3);
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedAsync(SampleDataSeedOptions options)
    {
        await using var context = _db.CreateContext();
        var seeder = new SampleDataSeeder(context, _hasher, _db.TimeProvider, Options.Create(options), _logger);
        await seeder.SeedAsync(TestContext.Current.CancellationToken);
    }

    private async Task AddAsync(object entity)
    {
        await using var context = _db.CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static SampleDataSeedOptions CreateOptions(string password, bool enabled = true) => new()
    {
        Enabled = enabled,
        Password = password,
    };
}
