using BuildingBlocks.Domain;
using Dispensario.Domain.Doctors;

namespace Dispensario.Domain.UnitTests.Doctors;

public sealed class ScheduleBlockTests
{
    [Fact]
    public void Create_WithValidData_SetsFields()
    {
        var block = ScheduleBlock.Create(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 30));

        block.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        block.StartTime.ShouldBe(new TimeOnly(9, 0));
        block.EndTime.ShouldBe(new TimeOnly(12, 30));
    }

    [Fact]
    public void Create_WithUndefinedDayOfWeek_ThrowsDomainException()
    {
        var act = () => ScheduleBlock.Create((DayOfWeek)7, new TimeOnly(9, 0), new TimeOnly(12, 0));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Day of week is invalid.");
    }

    [Theory]
    [InlineData(9, 3, 0)]
    [InlineData(9, 0, 30)]
    public void Create_WithTimeOffStep_ThrowsDomainException(int hour, int minute, int second)
    {
        var act = () => ScheduleBlock.Create(DayOfWeek.Monday, new TimeOnly(hour, minute, second), new TimeOnly(12, 0));

        act.ShouldThrow<DomainException>().Message
            .ShouldBe("Schedule times must be whole minutes and a multiple of 5.");
    }

    [Fact]
    public void Create_WithEndTimeOffStep_ThrowsDomainException()
    {
        var act = () => ScheduleBlock.Create(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 7));

        act.ShouldThrow<DomainException>().Message
            .ShouldBe("Schedule times must be whole minutes and a multiple of 5.");
    }

    [Theory]
    [InlineData(12, 0)]
    [InlineData(11, 0)]
    public void Create_WithStartNotBeforeEnd_ThrowsDomainException(int startHour, int endHour)
    {
        var act = () => ScheduleBlock.Create(DayOfWeek.Monday, new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));

        act.ShouldThrow<DomainException>().Message.ShouldBe("Schedule block start time must be before end time.");
    }

    [Fact]
    public void DurationMinutes_ReturnsLengthOfBlock()
    {
        var block = Block(DayOfWeek.Monday, 9, 0, 10, 45);

        block.DurationMinutes.ShouldBe(105);
    }

    [Theory]
    [InlineData(11, 0, 13, 0)]   // solape parcial
    [InlineData(10, 0, 11, 0)]   // contenido
    [InlineData(9, 0, 12, 0)]    // idéntico
    [InlineData(8, 0, 13, 0)]    // lo contiene
    public void Overlaps_WithOverlappingBlockSameDay_ReturnsTrue(int startHour, int startMinute, int endHour, int endMinute)
    {
        var block = Block(DayOfWeek.Monday, 9, 0, 12, 0);

        block.Overlaps(Block(DayOfWeek.Monday, startHour, startMinute, endHour, endMinute)).ShouldBeTrue();
    }

    [Fact]
    public void Overlaps_WithContiguousBlock_ReturnsFalse()
    {
        var block = Block(DayOfWeek.Monday, 9, 0, 12, 0);

        block.Overlaps(Block(DayOfWeek.Monday, 12, 0, 14, 0)).ShouldBeFalse();
        block.Overlaps(Block(DayOfWeek.Monday, 7, 0, 9, 0)).ShouldBeFalse();
    }

    [Fact]
    public void Overlaps_WithSameHoursOtherDay_ReturnsFalse()
    {
        var block = Block(DayOfWeek.Monday, 9, 0, 12, 0);

        block.Overlaps(Block(DayOfWeek.Tuesday, 9, 0, 12, 0)).ShouldBeFalse();
    }

    [Fact]
    public void Contains_WithRangeInsideOrOnEdges_ReturnsTrue()
    {
        var block = Block(DayOfWeek.Monday, 9, 0, 12, 0);

        block.Contains(new TimeOnly(9, 0), new TimeOnly(12, 0)).ShouldBeTrue();
        block.Contains(new TimeOnly(10, 0), new TimeOnly(10, 30)).ShouldBeTrue();
    }

    [Fact]
    public void Contains_WithRangeOutside_ReturnsFalse()
    {
        var block = Block(DayOfWeek.Monday, 9, 0, 12, 0);

        block.Contains(new TimeOnly(8, 30), new TimeOnly(9, 30)).ShouldBeFalse();
        block.Contains(new TimeOnly(11, 45), new TimeOnly(12, 15)).ShouldBeFalse();
    }

    private static ScheduleBlock Block(DayOfWeek day, int startHour, int startMinute, int endHour, int endMinute) =>
        ScheduleBlock.Create(day, new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));
}
