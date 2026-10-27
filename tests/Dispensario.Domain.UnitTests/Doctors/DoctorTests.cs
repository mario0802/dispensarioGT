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

    [Fact]
    public void Create_WithValidData_HasEmptySchedule()
    {
        var doctor = CreateDoctor();

        doctor.Schedule.ShouldBeEmpty();
    }

    [Fact]
    public void SetWeeklySchedule_WithBlocks_ReplacesSchedule()
    {
        var doctor = CreateDoctor();
        doctor.SetWeeklySchedule([Block(DayOfWeek.Friday, 8, 0, 10, 0)]);
        var monday = Block(DayOfWeek.Monday, 9, 0, 12, 0);
        var tuesday = Block(DayOfWeek.Tuesday, 14, 0, 18, 0);

        doctor.SetWeeklySchedule([monday, tuesday]);

        doctor.Schedule.ShouldBe([monday, tuesday], ignoreOrder: true);
    }

    [Fact]
    public void SetWeeklySchedule_WithEmptyList_ClearsSchedule()
    {
        var doctor = CreateDoctor();
        doctor.SetWeeklySchedule([Block(DayOfWeek.Monday, 9, 0, 12, 0)]);

        doctor.SetWeeklySchedule([]);

        doctor.Schedule.ShouldBeEmpty();
    }

    [Fact]
    public void SetWeeklySchedule_WithContiguousBlocks_AcceptsBoth()
    {
        var doctor = CreateDoctor();

        doctor.SetWeeklySchedule([Block(DayOfWeek.Monday, 9, 0, 12, 0), Block(DayOfWeek.Monday, 12, 0, 14, 0)]);

        doctor.Schedule.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(11, 0, 13, 0)]
    [InlineData(9, 0, 12, 0)]
    public void SetWeeklySchedule_WithOverlappingBlocks_ThrowsAndKeepsCurrentSchedule(
        int startHour, int startMinute, int endHour, int endMinute)
    {
        var doctor = CreateDoctor();
        var current = Block(DayOfWeek.Friday, 8, 0, 10, 0);
        doctor.SetWeeklySchedule([current]);

        var act = () => doctor.SetWeeklySchedule(
            [Block(DayOfWeek.Monday, 9, 0, 12, 0), Block(DayOfWeek.Monday, startHour, startMinute, endHour, endMinute)]);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Schedule blocks must not overlap.");
        doctor.Schedule.ShouldBe([current]);
    }

    [Fact]
    public void SetWeeklySchedule_WithBlockShorterThanDuration_ThrowsDomainException()
    {
        var doctor = CreateDoctor(appointmentDurationMinutes: 30);

        var act = () => doctor.SetWeeklySchedule([Block(DayOfWeek.Monday, 9, 0, 9, 20)]);

        act.ShouldThrow<DomainException>().Message
            .ShouldBe("Each schedule block must last at least the appointment duration.");
        doctor.Schedule.ShouldBeEmpty();
    }

    [Fact]
    public void SetWeeklySchedule_WithBlockEqualToDuration_AcceptsBlock()
    {
        var doctor = CreateDoctor(appointmentDurationMinutes: 30);

        doctor.SetWeeklySchedule([Block(DayOfWeek.Monday, 9, 0, 9, 30)]);

        doctor.Schedule.Count.ShouldBe(1);
    }

    [Fact]
    public void SetWeeklySchedule_OnInactiveDoctor_ThrowsDomainException()
    {
        var doctor = CreateDoctor();
        doctor.Deactivate();

        var act = () => doctor.SetWeeklySchedule([Block(DayOfWeek.Monday, 9, 0, 12, 0)]);

        act.ShouldThrow<DomainException>().Message.ShouldBe("Doctor is inactive.");
    }

    [Fact]
    public void Update_WithDurationLongerThanScheduleBlock_ThrowsAndKeepsCurrentData()
    {
        var doctor = CreateDoctor(appointmentDurationMinutes: 30);
        doctor.SetWeeklySchedule([Block(DayOfWeek.Monday, 9, 0, 12, 0), Block(DayOfWeek.Tuesday, 9, 0, 9, 40)]);

        var act = () => doctor.Update(doctor.FullName, "OTHER", doctor.Specialty, doctor.Email, null, 45);

        act.ShouldThrow<DomainException>().Message
            .ShouldBe("Appointment duration cannot be longer than a schedule block.");
        doctor.LicenseNumber.ShouldBe("MP-123");
        doctor.AppointmentDurationMinutes.ShouldBe(30);
    }

    [Fact]
    public void Update_WithDurationEqualToShortestBlock_UpdatesDuration()
    {
        var doctor = CreateDoctor(appointmentDurationMinutes: 30);
        doctor.SetWeeklySchedule([Block(DayOfWeek.Monday, 9, 0, 12, 0), Block(DayOfWeek.Tuesday, 9, 0, 9, 40)]);

        doctor.Update(doctor.FullName, doctor.LicenseNumber, doctor.Specialty, doctor.Email, null, 40);

        doctor.AppointmentDurationMinutes.ShouldBe(40);
    }

    // 2026-11-02 es lunes
    [Theory]
    [InlineData(10, 0, 10, 30)]   // dentro
    [InlineData(9, 0, 9, 30)]     // borde inicial
    [InlineData(11, 30, 12, 0)]   // borde final
    [InlineData(12, 0, 14, 0)]    // segundo bloque entero
    public void IsAvailableAt_WithRangeInsideBlock_ReturnsTrue(int startHour, int startMinute, int endHour, int endMinute)
    {
        var doctor = DoctorWithMondaySchedule();

        doctor.IsAvailableAt(Monday(startHour, startMinute), Monday(endHour, endMinute)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(8, 30, 9, 0)]     // antes del bloque
    [InlineData(8, 45, 9, 15)]    // empieza fuera
    [InlineData(14, 0, 14, 30)]   // después del último bloque
    [InlineData(11, 45, 12, 15)]  // salta entre dos bloques contiguos
    [InlineData(10, 30, 10, 0)]   // inicio posterior al fin
    [InlineData(10, 0, 10, 0)]    // inicio igual al fin
    public void IsAvailableAt_WithRangeOutsideBlock_ReturnsFalse(int startHour, int startMinute, int endHour, int endMinute)
    {
        var doctor = DoctorWithMondaySchedule();

        doctor.IsAvailableAt(Monday(startHour, startMinute), Monday(endHour, endMinute)).ShouldBeFalse();
    }

    [Fact]
    public void IsAvailableAt_OnDayWithoutBlocks_ReturnsFalse()
    {
        var doctor = DoctorWithMondaySchedule();
        var tuesday = Monday(10, 0).AddDays(1);

        doctor.IsAvailableAt(tuesday, tuesday.AddMinutes(30)).ShouldBeFalse();
    }

    [Fact]
    public void IsAvailableAt_WithRangeAcrossDates_ReturnsFalse()
    {
        var doctor = CreateDoctor();
        doctor.SetWeeklySchedule([Block(DayOfWeek.Monday, 0, 0, 23, 55), Block(DayOfWeek.Sunday, 0, 0, 23, 55)]);

        doctor.IsAvailableAt(Monday(0, 0).AddMinutes(-10), Monday(0, 20)).ShouldBeFalse();
    }

    [Fact]
    public void IsAvailableAt_OnInactiveDoctor_OnlyLooksAtSchedule()
    {
        var doctor = DoctorWithMondaySchedule();
        doctor.Deactivate();

        doctor.IsAvailableAt(Monday(10, 0), Monday(10, 30)).ShouldBeTrue();
    }

    private static Doctor DoctorWithMondaySchedule()
    {
        var doctor = CreateDoctor();
        doctor.SetWeeklySchedule([Block(DayOfWeek.Monday, 9, 0, 12, 0), Block(DayOfWeek.Monday, 12, 0, 14, 0)]);
        return doctor;
    }

    private static DateTime Monday(int hour, int minute) => new(2026, 11, 2, hour, minute, 0, DateTimeKind.Unspecified);

    private static ScheduleBlock Block(DayOfWeek day, int startHour, int startMinute, int endHour, int endMinute) =>
        ScheduleBlock.Create(day, new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));

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
