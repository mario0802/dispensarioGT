using BuildingBlocks.Domain;
using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Dispensario.Domain.Users.Events;

namespace Dispensario.Domain.UnitTests.Users;

public sealed class UserTests
{
    private const string PasswordHash = "$2a$11$hash";

    [Fact]
    public void Create_WithValidData_ReturnsActiveUserWithoutDoctor()
    {
        var email = Email.Create("ana@mail.com");
        var fullName = FullName.Create("Ana", "Pérez");
        var identification = Document.Create(DocumentType.NationalId, "12345678");
        var phone = PhoneNumber.Create("+51 999 888 777");

        var user = User.Create(email, PasswordHash, fullName, identification, phone, Role.Receptionist);

        user.Id.Value.ShouldNotBe(Guid.Empty);
        user.Email.ShouldBe(email);
        user.PasswordHash.ShouldBe(PasswordHash);
        user.FullName.ShouldBe(fullName);
        user.Identification.ShouldBe(identification);
        user.Phone.ShouldBe(phone);
        user.Role.ShouldBe(Role.Receptionist);
        user.DoctorId.ShouldBeNull();
        user.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithValidData_RaisesUserCreatedDomainEvent()
    {
        var user = CreateUser(Role.Admin);

        user.DomainEvents.ShouldBe([new UserCreatedDomainEvent(user.Id, user.Email, Role.Admin)]);
    }

    [Fact]
    public void Create_WithoutPhone_LeavesPhoneNull()
    {
        var user = CreateUser(phone: null);

        user.Phone.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyPasswordHash_ThrowsDomainException(string? passwordHash)
    {
        var act = () => User.Create(
            Email.Create("ana@mail.com"),
            passwordHash!,
            FullName.Create("Ana", "Pérez"),
            Document.Create(DocumentType.NationalId, "12345678"),
            null,
            Role.Admin);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Password hash is required.");
    }

    [Fact]
    public void Create_WithUndefinedRole_ThrowsDomainException()
    {
        var act = () => CreateUser((Role)99);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Role '99' is not valid.");
    }

    [Fact]
    public void ChangePassword_WithNewHash_ReplacesHash()
    {
        var user = CreateUser();

        user.ChangePassword("$2a$11$other");

        user.PasswordHash.ShouldBe("$2a$11$other");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ChangePassword_WithEmptyHash_ThrowsDomainException(string? newHash)
    {
        var user = CreateUser();

        var act = () => user.ChangePassword(newHash!);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Password hash is required.");
        user.PasswordHash.ShouldBe(PasswordHash);
    }

    [Fact]
    public void UpdateProfile_WithNewData_ReplacesNameAndPhone()
    {
        var user = CreateUser(phone: null);
        var fullName = FullName.Create("Luis", "Gómez");
        var phone = PhoneNumber.Create("999-888-777");

        user.UpdateProfile(fullName, phone);

        user.FullName.ShouldBe(fullName);
        user.Phone.ShouldBe(phone);
    }

    [Fact]
    public void UpdateProfile_WithNullPhone_RemovesPhone()
    {
        var user = CreateUser(phone: PhoneNumber.Create("999888777"));

        user.UpdateProfile(user.FullName, null);

        user.Phone.ShouldBeNull();
    }

    [Fact]
    public void Deactivate_WhenActive_SetsInactive()
    {
        var user = CreateUser();

        user.Deactivate();

        user.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Deactivate_WhenInactive_ThrowsDomainException()
    {
        var user = CreateUser();
        user.Deactivate();

        var act = () => user.Deactivate();

        act.ShouldThrow<DomainException>().Message.ShouldBe("User is already inactive.");
    }

    [Fact]
    public void Activate_WhenInactive_SetsActive()
    {
        var user = CreateUser();
        user.Deactivate();

        user.Activate();

        user.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Activate_WhenActive_ThrowsDomainException()
    {
        var user = CreateUser();

        var act = () => user.Activate();

        act.ShouldThrow<DomainException>().Message.ShouldBe("User is already active.");
    }

    [Fact]
    public void LinkDoctor_WithDoctorRole_SetsDoctorId()
    {
        var user = CreateUser(Role.Doctor);
        var doctorId = DoctorId.New();

        user.LinkDoctor(doctorId);

        user.DoctorId.ShouldBe(doctorId);
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Receptionist)]
    public void LinkDoctor_WithOtherRole_ThrowsDomainException(Role role)
    {
        var user = CreateUser(role);

        var act = () => user.LinkDoctor(DoctorId.New());

        act.ShouldThrow<DomainException>().Message.ShouldBe("Only users with the Doctor role can be linked to a doctor.");
        user.DoctorId.ShouldBeNull();
    }

    [Fact]
    public void LinkDoctor_WhenAlreadyLinked_ThrowsDomainException()
    {
        var user = CreateUser(Role.Doctor);
        var doctorId = DoctorId.New();
        user.LinkDoctor(doctorId);

        var act = () => user.LinkDoctor(DoctorId.New());

        act.ShouldThrow<DomainException>().Message.ShouldBe("User is already linked to a doctor.");
        user.DoctorId.ShouldBe(doctorId);
    }

    private static User CreateUser(Role role = Role.Receptionist, PhoneNumber? phone = null) =>
        User.Create(
            Email.Create("ana@mail.com"),
            PasswordHash,
            FullName.Create("Ana", "Pérez"),
            Document.Create(DocumentType.NationalId, "12345678"),
            phone,
            role);
}
