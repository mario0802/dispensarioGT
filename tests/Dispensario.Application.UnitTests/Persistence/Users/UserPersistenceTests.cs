using BuildingBlocks.Domain;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Dispensario.Domain.Users.Events;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Persistence.Users;

public sealed class UserPersistenceTests : IDisposable
{
    private const string PasswordHash = "$2a$11$hash";

    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task SaveChangesAsync_WithAllFields_ReadsBackEqualUser()
    {
        var user = CreateUser(
            email: "ana@mail.com",
            document: Document.Create(DocumentType.Passport, "AB123456"),
            phone: PhoneNumber.Create("+51 999 888 777"),
            role: Role.Doctor);
        var doctorId = DoctorId.New();
        user.LinkDoctor(doctorId);
        await SaveAsync(user);

        await using var context = _db.CreateContext();
        var loaded = await context.Users.SingleAsync(TestContext.Current.CancellationToken);

        loaded.Id.ShouldBe(user.Id);
        loaded.Email.ShouldBe(user.Email);
        loaded.PasswordHash.ShouldBe(PasswordHash);
        loaded.FullName.ShouldBe(user.FullName);
        loaded.Identification.ShouldBe(user.Identification);
        loaded.Phone.ShouldBe(user.Phone);
        loaded.Role.ShouldBe(Role.Doctor);
        loaded.DoctorId.ShouldBe(doctorId);
        loaded.IsActive.ShouldBeTrue();
        loaded.CreatedAtUtc.ShouldBe(DispensarioTestDb.StartTime.UtcDateTime);
        loaded.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task SaveChangesAsync_WithoutPhoneAndDoctor_ReadsBackNulls()
    {
        await SaveAsync(CreateUser(phone: null));

        await using var context = _db.CreateContext();
        var loaded = await context.Users.SingleAsync(TestContext.Current.CancellationToken);

        loaded.Phone.ShouldBeNull();
        loaded.DoctorId.ShouldBeNull();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ByEmail_FindsUser()
    {
        await SaveAsync(CreateUser(email: "ana@mail.com"));
        var email = Email.Create("ANA@mail.com");

        await using var context = _db.CreateContext();
        var loaded = await context.Users.FirstOrDefaultAsync(u => u.Email == email, TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_WithDuplicateEmail_ThrowsDbUpdateException()
    {
        await SaveAsync(CreateUser(email: "ana@mail.com", document: Document.Create(DocumentType.NationalId, "111")));

        var act = () => SaveAsync(CreateUser(email: "ana@mail.com", document: Document.Create(DocumentType.NationalId, "222")));

        await act.ShouldThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsync_WithDuplicateDocument_ThrowsDbUpdateException()
    {
        await SaveAsync(CreateUser(email: "ana@mail.com", document: Document.Create(DocumentType.NationalId, "12345678")));

        var act = () => SaveAsync(CreateUser(email: "luis@mail.com", document: Document.Create(DocumentType.NationalId, "12345678")));

        await act.ShouldThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsync_WithSameNumberDifferentType_SavesBothUsers()
    {
        await SaveAsync(CreateUser(email: "ana@mail.com", document: Document.Create(DocumentType.NationalId, "12345678")));
        await SaveAsync(CreateUser(email: "luis@mail.com", document: Document.Create(DocumentType.Passport, "12345678")));

        await using var context = _db.CreateContext();
        var count = await context.Users.CountAsync(TestContext.Current.CancellationToken);

        count.ShouldBe(2);
    }

    [Fact]
    public async Task SaveChangesAsync_WithEnums_StoresThemAsText()
    {
        await SaveAsync(CreateUser(document: Document.Create(DocumentType.Passport, "AB123"), role: Role.Receptionist));

        await using var command = _db.Connection.CreateCommand();
        command.CommandText = "SELECT Role, Identification_Type FROM Users";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await reader.ReadAsync(TestContext.Current.CancellationToken);

        reader.GetString(0).ShouldBe("Receptionist");
        reader.GetString(1).ShouldBe("Passport");
    }

    [Fact]
    public async Task EnsureCreated_UsersTable_HasExpectedColumns()
    {
        var columns = new List<string>();
        await using var command = _db.Connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('Users')";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            columns.Add(reader.GetString(0));

        columns.ShouldBe(
            [
                "Id", "Email", "PasswordHash", "FullName_FirstName", "FullName_LastName",
                "Identification_Type", "Identification_Number", "Phone", "Role", "DoctorId", "IsActive",
                "CreatedAtUtc", "CreatedBy", "LastModifiedAtUtc", "LastModifiedBy"
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNewUser_PublishesUserCreatedDomainEvent()
    {
        var user = CreateUser(role: Role.Admin);

        await SaveAsync(user);

        await _db.Publisher.Received(1).Publish(
            Arg.Is<IDomainEvent>(e => e.Equals(new UserCreatedDomainEvent(user.Id, user.Email, Role.Admin))),
            Arg.Any<CancellationToken>());
    }

    public void Dispose() => _db.Dispose();

    private static User CreateUser(
        string email = "ana@mail.com",
        Document? document = null,
        PhoneNumber? phone = null,
        Role role = Role.Receptionist) =>
        User.Create(
            Email.Create(email),
            PasswordHash,
            FullName.Create("Ana", "Pérez"),
            document ?? Document.Create(DocumentType.NationalId, "12345678"),
            phone,
            role);

    private async Task SaveAsync(User user)
    {
        await using var context = _db.CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
