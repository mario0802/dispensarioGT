using BuildingBlocks.Domain;
using Dispensario.Domain.Common;
using Dispensario.Domain.Patients;

namespace Dispensario.Domain.UnitTests.Patients;

public sealed class PatientTests
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    [Fact]
    public void Create_WithAllFields_ReturnsActivePatient()
    {
        var document = Document.Create(DocumentType.NationalId, "0102030405");
        var fullName = FullName.Create("Ana", "Pérez");
        var email = Email.Create("ana@mail.com");
        var phone = PhoneNumber.Create("0991234567");
        var address = Address.Create("Av. Amazonas 123", "Quito");

        var patient = Patient.Create(
            document, fullName, new DateOnly(1990, 5, 14), Gender.Female,
            email, phone, address, BloodType.OPositive, "Penicillin", Today);

        patient.Id.Value.ShouldNotBe(Guid.Empty);
        patient.Document.ShouldBe(document);
        patient.FullName.ShouldBe(fullName);
        patient.BirthDate.ShouldBe(new DateOnly(1990, 5, 14));
        patient.Gender.ShouldBe(Gender.Female);
        patient.Email.ShouldBe(email);
        patient.Phone.ShouldBe(phone);
        patient.Address.ShouldBe(address);
        patient.BloodType.ShouldBe(BloodType.OPositive);
        patient.Allergies.ShouldBe("Penicillin");
        patient.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithValidData_RaisesNoDomainEvents()
    {
        var patient = CreatePatient();

        patient.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Create_WithAllergiesSurroundedBySpaces_TrimsAllergies()
    {
        var patient = CreatePatient(allergies: "  Penicillin  ");

        patient.Allergies.ShouldBe("Penicillin");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithBlankAllergies_StoresNull(string? allergies)
    {
        var patient = CreatePatient(allergies: allergies);

        patient.Allergies.ShouldBeNull();
    }

    [Fact]
    public void Create_WithTooLongAllergies_ThrowsDomainException()
    {
        var act = () => CreatePatient(allergies: new string('a', Patient.AllergiesMaxLength + 1));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Allergies must be at most 1000 characters.");
    }

    [Fact]
    public void Create_BornToday_IsValid()
    {
        var patient = CreatePatient(birthDate: Today);

        patient.BirthDate.ShouldBe(Today);
    }

    [Fact]
    public void Create_BornTomorrow_ThrowsDomainException()
    {
        var act = () => CreatePatient(birthDate: Today.AddDays(1));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Birth date cannot be in the future.");
    }

    [Fact]
    public void Create_WithUndefinedGender_ThrowsDomainException()
    {
        var act = () => CreatePatient(gender: (Gender)99);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Gender is invalid.");
    }

    [Fact]
    public void Create_WithUndefinedBloodType_ThrowsDomainException()
    {
        var act = () => CreatePatient(bloodType: (BloodType)99);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Blood type is invalid.");
    }

    [Fact]
    public void Update_WithNewData_ReplacesAllFields()
    {
        var patient = CreatePatient();
        var document = Document.Create(DocumentType.Passport, "AB123");
        var fullName = FullName.Create("Carla", "Andrade");
        var email = Email.Create("carla@mail.com");
        var phone = PhoneNumber.Create("0987654321");
        var address = Address.Create("Calle 10", "Cuenca", "Azuay", "Casa azul");

        patient.Update(
            document, fullName, new DateOnly(2001, 1, 2), Gender.Other,
            email, phone, address, BloodType.ABNegative, "Latex", Today);

        patient.Document.ShouldBe(document);
        patient.FullName.ShouldBe(fullName);
        patient.BirthDate.ShouldBe(new DateOnly(2001, 1, 2));
        patient.Gender.ShouldBe(Gender.Other);
        patient.Email.ShouldBe(email);
        patient.Phone.ShouldBe(phone);
        patient.Address.ShouldBe(address);
        patient.BloodType.ShouldBe(BloodType.ABNegative);
        patient.Allergies.ShouldBe("Latex");
    }

    [Fact]
    public void Update_WithNullOptionals_ClearsThem()
    {
        var patient = CreatePatient(
            email: Email.Create("ana@mail.com"),
            phone: PhoneNumber.Create("0991234567"),
            address: Address.Create("Av. Amazonas 123", "Quito"),
            bloodType: BloodType.APositive,
            allergies: "Penicillin");

        patient.Update(patient.Document, patient.FullName, patient.BirthDate, patient.Gender,
            null, null, null, null, null, Today);

        patient.Email.ShouldBeNull();
        patient.Phone.ShouldBeNull();
        patient.Address.ShouldBeNull();
        patient.BloodType.ShouldBeNull();
        patient.Allergies.ShouldBeNull();
    }

    [Fact]
    public void Update_WithFutureBirthDate_ThrowsAndKeepsData()
    {
        var patient = CreatePatient();
        var originalName = patient.FullName;

        var act = () => patient.Update(patient.Document, FullName.Create("Otro", "Nombre"), Today.AddDays(1),
            patient.Gender, null, null, null, null, null, Today);

        act.ShouldThrow<DomainException>();
        patient.FullName.ShouldBe(originalName);
    }

    [Fact]
    public void Update_OnInactivePatient_UpdatesData()
    {
        var patient = CreatePatient();
        patient.Deactivate();

        patient.Update(patient.Document, FullName.Create("Carla", "Andrade"), patient.BirthDate, patient.Gender,
            null, null, null, null, null, Today);

        patient.FullName.LastName.ShouldBe("Andrade");
    }

    [Fact]
    public void Deactivate_ActivePatient_SetsInactive()
    {
        var patient = CreatePatient();

        patient.Deactivate();

        patient.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Deactivate_InactivePatient_ThrowsDomainException()
    {
        var patient = CreatePatient();
        patient.Deactivate();

        var act = patient.Deactivate;

        act.ShouldThrow<DomainException>().Message.ShouldBe("Patient is already inactive.");
    }

    [Fact]
    public void Activate_InactivePatient_SetsActive()
    {
        var patient = CreatePatient();
        patient.Deactivate();

        patient.Activate();

        patient.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Activate_ActivePatient_ThrowsDomainException()
    {
        var patient = CreatePatient();

        var act = patient.Activate;

        act.ShouldThrow<DomainException>().Message.ShouldBe("Patient is already active.");
    }

    private static Patient CreatePatient(
        DateOnly? birthDate = null,
        Gender gender = Gender.Female,
        Email? email = null,
        PhoneNumber? phone = null,
        Address? address = null,
        BloodType? bloodType = null,
        string? allergies = null) => Patient.Create(
        Document.Create(DocumentType.NationalId, "0102030405"),
        FullName.Create("Ana", "Pérez"),
        birthDate ?? new DateOnly(1990, 5, 14),
        gender,
        email,
        phone,
        address,
        bloodType,
        allergies,
        Today);
}
