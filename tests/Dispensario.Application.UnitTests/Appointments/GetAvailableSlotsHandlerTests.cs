using BuildingBlocks.Exceptions;
using Dispensario.Application.Appointments.Dtos;
using Dispensario.Application.Appointments.Queries.GetAvailableSlots;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;

namespace Dispensario.Application.UnitTests.Appointments;

// El reloj falso marca 2026-10-26 12:00 UTC (lunes, 07:00 locales en Guayaquil, UTC-5).
// El doctor atiende lunes y martes de 09:00 a 12:00 en citas de 30 minutos
public sealed class GetAvailableSlotsHandlerTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 26);
    private static readonly DateOnly Tuesday = new(2026, 11, 3);

    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_FutureDay_ReturnsAllScheduleSlots()
    {
        var doctor = await SeedDoctorAsync();

        var result = await HandleAsync(doctor.Id.Value, Tuesday);

        result.DoctorId.ShouldBe(doctor.Id.Value);
        result.Date.ShouldBe(Tuesday);
        result.DurationMinutes.ShouldBe(30);
        StartTimes(result).ShouldBe(
        [
            new TimeOnly(9, 0), new TimeOnly(9, 30), new TimeOnly(10, 0),
            new TimeOnly(10, 30), new TimeOnly(11, 0), new TimeOnly(11, 30),
        ]);
        result.Slots[0].ShouldBe(new AvailableSlotDto(Local(Tuesday, 9, 0), Local(Tuesday, 9, 30)));
    }

    [Fact]
    public async Task Handle_WithScheduledAndConfirmedAppointments_ExcludesTheirSlots()
    {
        var doctor = await SeedDoctorAsync();
        var confirmed = TestAppointments.Create(doctorId: doctor.Id, startUtc: Utc(Tuesday, 10, 0));
        confirmed.Confirm(TestAppointments.NowUtc);
        await AddAsync(TestAppointments.Create(doctorId: doctor.Id, startUtc: Utc(Tuesday, 9, 0)), confirmed);

        var result = await HandleAsync(doctor.Id.Value, Tuesday);

        StartTimes(result).ShouldBe(
            [new TimeOnly(9, 30), new TimeOnly(10, 30), new TimeOnly(11, 0), new TimeOnly(11, 30)]);
    }

    [Fact]
    public async Task Handle_WithCancelledAppointment_KeepsItsSlot()
    {
        var doctor = await SeedDoctorAsync();
        var cancelled = TestAppointments.Create(doctorId: doctor.Id, startUtc: Utc(Tuesday, 9, 0));
        cancelled.Cancel("El paciente no puede venir", TestAppointments.NowUtc);
        await AddAsync(cancelled);

        var result = await HandleAsync(doctor.Id.Value, Tuesday);

        StartTimes(result).ShouldContain(new TimeOnly(9, 0));
        result.Slots.Count.ShouldBe(6);
    }

    [Fact]
    public async Task Handle_WithContiguousAppointment_KeepsNeighbourSlots()
    {
        // La cita 09:30–10:00 solo quita su slot: 09:00 y 10:00 son contiguos
        var doctor = await SeedDoctorAsync();
        await AddAsync(TestAppointments.Create(doctorId: doctor.Id, startUtc: Utc(Tuesday, 9, 30)));

        var result = await HandleAsync(doctor.Id.Value, Tuesday);

        StartTimes(result).ShouldBe(
            [new TimeOnly(9, 0), new TimeOnly(10, 0), new TimeOnly(10, 30), new TimeOnly(11, 0), new TimeOnly(11, 30)]);
    }

    [Fact]
    public async Task Handle_WithPartiallyOverlappingAppointment_ExcludesEverySlotItTouches()
    {
        // Una cita de 45 minutos (09:15–10:00) toca los slots de 09:00 y 09:30
        var doctor = await SeedDoctorAsync();
        await AddAsync(TestAppointments.Create(doctorId: doctor.Id, startUtc: Utc(Tuesday, 9, 15), durationMinutes: 45));

        var result = await HandleAsync(doctor.Id.Value, Tuesday);

        StartTimes(result).ShouldBe(
            [new TimeOnly(10, 0), new TimeOnly(10, 30), new TimeOnly(11, 0), new TimeOnly(11, 30)]);
    }

    [Fact]
    public async Task Handle_WithOtherDoctorAppointment_KeepsAllSlots()
    {
        var doctor = await SeedDoctorAsync();
        await AddAsync(TestAppointments.Create(doctorId: DoctorId.New(), startUtc: Utc(Tuesday, 9, 0)));

        var result = await HandleAsync(doctor.Id.Value, Tuesday);

        result.Slots.Count.ShouldBe(6);
    }

    [Fact]
    public async Task Handle_Today_ReturnsOnlySlotsStartingAfterNow()
    {
        // 15:00 UTC son las 10:00 locales: el slot de las 10:00 empieza justo ahora y ya no se ofrece
        var doctor = await SeedDoctorAsync();
        _db.TimeProvider.SetUtcNow(new DateTimeOffset(2026, 10, 26, 15, 0, 0, TimeSpan.Zero));

        var result = await HandleAsync(doctor.Id.Value, Today);

        StartTimes(result).ShouldBe([new TimeOnly(10, 30), new TimeOnly(11, 0), new TimeOnly(11, 30)]);
    }

    [Fact]
    public async Task Handle_WithPastDate_ThrowsBadRequestException()
    {
        var doctor = await SeedDoctorAsync();

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(doctor.Id.Value, Today.AddDays(-1)));

        exception.Message.ShouldBe("Date cannot be in the past.");
    }

    [Fact]
    public async Task Handle_WithUnknownDoctor_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Guid.CreateVersion7(), Tuesday));
    }

    [Fact]
    public async Task Handle_WithInactiveDoctor_ThrowsBadRequestException()
    {
        var doctor = await SeedDoctorAsync(isActive: false);

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(doctor.Id.Value, Tuesday));

        exception.Message.ShouldBe("Doctor is inactive.");
    }

    [Fact]
    public async Task Handle_DayWithoutSchedule_ReturnsEmptySlots()
    {
        var doctor = await SeedDoctorAsync();

        // 4 de noviembre de 2026: miércoles
        var result = await HandleAsync(doctor.Id.Value, new DateOnly(2026, 11, 4));

        result.Slots.ShouldBeEmpty();
        result.DurationMinutes.ShouldBe(30);
    }

    public void Dispose() => _db.Dispose();

    private static DateTime Local(DateOnly date, int hour, int minute) => date.ToDateTime(new TimeOnly(hour, minute));

    // Hora local del dispensario (UTC-5) expresada en UTC
    private static DateTime Utc(DateOnly date, int hour, int minute) =>
        DateTime.SpecifyKind(Local(date, hour, minute).AddHours(5), DateTimeKind.Utc);

    private static IEnumerable<TimeOnly> StartTimes(AvailableSlotsDto result) =>
        result.Slots.Select(s => TimeOnly.FromDateTime(s.StartLocal));

    private async Task<Doctor> SeedDoctorAsync(bool isActive = true)
    {
        var doctor = TestDoctors.Create();
        doctor.SetWeeklySchedule(
        [
            ScheduleBlock.Create(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
            ScheduleBlock.Create(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
        ]);
        if (!isActive)
            doctor.Deactivate();

        await using var context = _db.CreateContext();
        context.Doctors.Add(doctor);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return doctor;
    }

    private async Task AddAsync(params Appointment[] appointments)
    {
        await using var context = _db.CreateContext();
        context.Appointments.AddRange(appointments);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<AvailableSlotsDto> HandleAsync(Guid doctorId, DateOnly date)
    {
        await using var context = _db.CreateContext();
        return await new GetAvailableSlotsHandler(context, _db.ClinicTimeZone, _db.TimeProvider)
            .Handle(new GetAvailableSlotsQuery(doctorId, date), TestContext.Current.CancellationToken);
    }
}
