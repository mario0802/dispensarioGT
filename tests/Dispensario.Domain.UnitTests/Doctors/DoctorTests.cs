using BuildingBlocks.Domain;
using Dispensario.Domain.Common;
using Dispensario.Domain.Doctors;

namespace Dispensario.Domain.UnitTests.Doctors;

public sealed class DoctorTests
{
    [Fact]
    public void Create_WithValidData_ReturnsActiveDoctorWithNormalizedFields()
    {
        var fullName = FullName.Create("Luis", "Mora");
        var email = Email.Create("luis.mora@mail.com");
        var phone = PhoneNumber.Create("0991234567");

        var doctor = Doctor.Create(fullName, " mp-123 ", "  Pediatría  ", email, phone, 30);

        doctor.Id.Value.ShouldNotBe(Guid.Empty);
        doctor.FullName.ShouldBe(fullName);
        doctor.LicenseNumber.ShouldBe("MP-123");
        doctor.Specialty.ShouldBe("Pediatría");
        doctor.Email.ShouldBe(email);
        doctor.Phone.ShouldBe(phone);
        doctor.AppointmentDurationMinutes.ShouldBe(30);
        doctor.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithValidData_RaisesNoDomainEvents()
    {
        var doctor = CreateDoctor();

        doctor.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyLicenseNumber_ThrowsDomainException(string? licenseNumber)
    {
        var act = () => CreateDoctor(licenseNumber: licenseNumber!);

        act.ShouldThrow<DomainException>().Message.ShouldBe("License number is required.");
    }

    [Fact]
    public void Create_WithTooLongLicenseNumber_ThrowsDomainException()
    {
        var act = () => CreateDoctor(licenseNumber: new string('A', Doctor.LicenseNumberMaxLength + 1));

        act.ShouldThrow<DomainException>().Message.ShouldBe("License number must be at most 30 characters.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptySpecialty_ThrowsDomainException(string? specialty)
    {
        var act = () => CreateDoctor(specialty: specialty!);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Specialty is required.");
    }

    [Fact]
    public void Create_WithTooLongSpecialty_ThrowsDomainException()
    {
        var act = () => CreateDoctor(specialty: new string('a', Doctor.SpecialtyMaxLength + 1));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Specialty must be at most 100 characters.");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(125)]
    [InlineData(32)]
    [InlineData(0)]
    public void Create_WithInvalidAppointmentDuration_ThrowsDomainException(int minutes)
    {
        var act = () => CreateDoctor(appointmentDurationMinutes: minutes);

        act.ShouldThrow<DomainException>().Message
            .ShouldBe("Appointment duration must be between 10 and 120 minutes and a multiple of 5.");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(120)]
    public void Create_WithValidAppointmentDuration_SetsDuration(int minutes)
    {
        var doctor = CreateDoctor(appointmentDurationMinutes: minutes);

        doctor.AppointmentDurationMinutes.ShouldBe(minutes);
    }

    [Fact]
    public void Update_WithNewData_ReplacesAllFields()
    {
        var doctor = CreateDoctor();
        var fullName = FullName.Create("Beatriz", "Andrade");
        var email = Email.Create("bea@mail.com");
        var phone = PhoneNumber.Create("+593 99 000 1111");

        doctor.Update(fullName, "cm-9", "Cardiología", email, phone, 45);

        doctor.FullName.ShouldBe(fullName);
        doctor.LicenseNumber.ShouldBe("CM-9");
        doctor.Specialty.ShouldBe("Cardiología");
        doctor.Email.ShouldBe(email);
        doctor.Phone.ShouldBe(phone);
        doctor.AppointmentDurationMinutes.ShouldBe(45);
    }

    [Fact]
    public void Update_WithNullPhone_ClearsPhone()
    {
        var doctor = CreateDoctor(phone: PhoneNumber.Create("0991234567"));

        doctor.Update(doctor.FullName, doctor.LicenseNumber, doctor.Specialty, doctor.Email, null, 30);

        doctor.Phone.ShouldBeNull();
    }

    [Fact]
    public void Update_WithInvalidDuration_ThrowsAndKeepsCurrentData()
    {
        var doctor = CreateDoctor();

        var act = () => doctor.Update(doctor.FullName, "OTHER", doctor.Specialty, doctor.Email, null, 32);

        act.ShouldThrow<DomainException>();
        doctor.LicenseNumber.ShouldBe("MP-123");
        doctor.AppointmentDurationMinutes.ShouldBe(30);
    }

    [Fact]
    public void Update_OnInactiveDoctor_UpdatesData()
    {
        var doctor = CreateDoctor();
        doctor.Deactivate();

        doctor.Update(doctor.FullName, doctor.LicenseNumber, "Dermatología", doctor.Email, null, 20);

        doctor.Specialty.ShouldBe("Dermatología");
    }

    [Fact]
    public void Deactivate_ActiveDoctor_SetsInactive()
    {
        var doctor = CreateDoctor();

        doctor.Deactivate();

        doctor.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Deactivate_InactiveDoctor_ThrowsDomainException()
    {
        var doctor = CreateDoctor();
        doctor.Deactivate();

        var act = doctor.Deactivate;

        act.ShouldThrow<DomainException>().Message.ShouldBe("Doctor is already inactive.");
    }

    [Fact]
    public void Activate_InactiveDoctor_SetsActive()
    {
        var doctor = CreateDoctor();
        doctor.Deactivate();

        doctor.Activate();

        doctor.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Activate_ActiveDoctor_ThrowsDomainException()
    {
        var doctor = CreateDoctor();

        var act = doctor.Activate;

        act.ShouldThrow<DomainException>().Message.ShouldBe("Doctor is already active.");
    }

    private static Doctor CreateDoctor(
        string licenseNumber = "MP-123",
        string specialty = "Pediatría",
        PhoneNumber? phone = null,
        int appointmentDurationMinutes = 30) =>
        Doctor.Create(
            FullName.Create("Luis", "Mora"),
            licenseNumber,
            specialty,
            Email.Create("luis.mora@mail.com"),
            phone,
            appointmentDurationMinutes);
}
