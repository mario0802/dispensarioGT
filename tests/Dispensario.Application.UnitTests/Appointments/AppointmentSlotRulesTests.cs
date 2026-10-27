using Dispensario.Application.Appointments.Common;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Domain.Doctors;

namespace Dispensario.Application.UnitTests.Appointments;

// El 3 de noviembre de 2026 es martes
public sealed class AppointmentSlotRulesTests
{
    private static readonly DateOnly Tuesday = new(2026, 11, 3);

    [Fact]
    public void GetScheduleSlots_WithThreeHourBlock_ReturnsSlotsFromBlockStart()
    {
        var doctor = CreateDoctor(Block(DayOfWeek.Tuesday, 9, 0, 12, 0));

        var slots = AppointmentSlotRules.GetScheduleSlots(doctor, Tuesday);

        slots.Select(s => s.LocalStart.TimeOfDay).ShouldBe(
        [
            new TimeSpan(9, 0, 0), new TimeSpan(9, 30, 0), new TimeSpan(10, 0, 0),
            new TimeSpan(10, 30, 0), new TimeSpan(11, 0, 0), new TimeSpan(11, 30, 0),
        ]);
        slots.ShouldAllBe(s => s.LocalEnd - s.LocalStart == TimeSpan.FromMinutes(30));
        slots.ShouldAllBe(s => s.LocalStart.Kind == DateTimeKind.Unspecified);
        slots.ShouldAllBe(s => DateOnly.FromDateTime(s.LocalStart) == Tuesday);
    }

    [Fact]
    public void GetScheduleSlots_WithBlockRemainder_DiscardsPartialSlot()
    {
        var doctor = CreateDoctor(Block(DayOfWeek.Tuesday, 9, 0, 12, 10));

        var slots = AppointmentSlotRules.GetScheduleSlots(doctor, Tuesday);

        slots.Count.ShouldBe(6);
        slots[^1].ShouldBe((Tuesday.ToDateTime(new TimeOnly(11, 30)), Tuesday.ToDateTime(new TimeOnly(12, 0))));
    }

    [Fact]
    public void GetScheduleSlots_WithBlockStartingOffHour_AlignsFromBlockStart()
    {
        var doctor = CreateDoctor(Block(DayOfWeek.Tuesday, 9, 10, 10, 10));

        var slots = AppointmentSlotRules.GetScheduleSlots(doctor, Tuesday);

        slots.Select(s => TimeOnly.FromDateTime(s.LocalStart)).ShouldBe([new TimeOnly(9, 10), new TimeOnly(9, 40)]);
    }

    [Fact]
    public void GetScheduleSlots_WithTwoBlocksSameDay_ReturnsSlotsOrderedByStart()
    {
        // Se cargan en orden inverso para comprobar que salen ordenados
        var doctor = CreateDoctor(Block(DayOfWeek.Tuesday, 15, 0, 16, 0), Block(DayOfWeek.Tuesday, 9, 0, 10, 0));

        var slots = AppointmentSlotRules.GetScheduleSlots(doctor, Tuesday);

        slots.Select(s => TimeOnly.FromDateTime(s.LocalStart)).ShouldBe(
            [new TimeOnly(9, 0), new TimeOnly(9, 30), new TimeOnly(15, 0), new TimeOnly(15, 30)]);
    }

    [Fact]
    public void GetScheduleSlots_WithoutBlocks_ReturnsEmpty()
    {
        var doctor = CreateDoctor();

        AppointmentSlotRules.GetScheduleSlots(doctor, Tuesday).ShouldBeEmpty();
    }

    [Fact]
    public void GetScheduleSlots_WithBlocksOnOtherDays_IgnoresThem()
    {
        var doctor = CreateDoctor(Block(DayOfWeek.Monday, 9, 0, 12, 0), Block(DayOfWeek.Wednesday, 9, 0, 12, 0));

        AppointmentSlotRules.GetScheduleSlots(doctor, Tuesday).ShouldBeEmpty();
    }

    [Fact]
    public void GetScheduleSlots_EverySlot_PassesEnsureFitsSchedule()
    {
        var doctor = CreateDoctor(
            durationMinutes: 20,
            blocks: [Block(DayOfWeek.Tuesday, 8, 5, 11, 55), Block(DayOfWeek.Tuesday, 14, 10, 17, 0)]);

        var slots = AppointmentSlotRules.GetScheduleSlots(doctor, Tuesday);

        slots.ShouldNotBeEmpty();
        foreach (var (localStart, localEnd) in slots)
            Should.NotThrow(() => AppointmentSlotRules.EnsureFitsSchedule(doctor, localStart, localEnd));
    }

    private static Doctor CreateDoctor(params ScheduleBlock[] blocks) =>
        CreateDoctor(Doctor.DefaultAppointmentDurationMinutes, blocks);

    private static Doctor CreateDoctor(int durationMinutes, ScheduleBlock[] blocks)
    {
        var doctor = TestDoctors.Create(appointmentDurationMinutes: durationMinutes);
        doctor.SetWeeklySchedule(blocks);
        return doctor;
    }

    private static ScheduleBlock Block(DayOfWeek day, int startHour, int startMinute, int endHour, int endMinute) =>
        ScheduleBlock.Create(day, new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));
}
