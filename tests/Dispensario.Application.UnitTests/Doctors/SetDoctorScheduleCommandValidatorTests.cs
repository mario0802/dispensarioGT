using Dispensario.Application.Doctors.Commands.SetDoctorSchedule;

namespace Dispensario.Application.UnitTests.Doctors;

public sealed class SetDoctorScheduleCommandValidatorTests
{
    private static readonly SetDoctorScheduleCommand ValidCommand = new(
        Guid.NewGuid(),
        [new ScheduleBlockInput(DayOfWeek.Monday, "09:00", "12:00"), new ScheduleBlockInput(DayOfWeek.Sunday, "23:00", "23:55")]);

    private readonly SetDoctorScheduleCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_IsValid()
    {
        var result = _validator.Validate(ValidCommand);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyBlocks_IsValid()
    {
        var result = _validator.Validate(ValidCommand with { Blocks = [] });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithEmptyDoctorId_HasDoctorIdError()
    {
        var result = _validator.Validate(ValidCommand with { DoctorId = Guid.Empty });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(SetDoctorScheduleCommand.DoctorId));
    }

    [Fact]
    public void Validate_WithNullBlocks_HasBlocksError()
    {
        var result = _validator.Validate(ValidCommand with { Blocks = null! });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(SetDoctorScheduleCommand.Blocks));
    }

    [Fact]
    public void Validate_WithNullBlock_IsInvalid()
    {
        var result = _validator.Validate(ValidCommand with { Blocks = [null!] });

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WithUndefinedDayOfWeek_HasDayOfWeekError()
    {
        var result = _validator.Validate(ValidCommand with { Blocks = [new ScheduleBlockInput((DayOfWeek)7, "09:00", "12:00")] });

        result.Errors.ShouldContain(error => error.PropertyName.EndsWith(nameof(ScheduleBlockInput.DayOfWeek)));
    }

    [Theory]
    [InlineData("9:00")]
    [InlineData("24:00")]
    [InlineData("09:00:00")]
    [InlineData("09:60")]
    [InlineData("")]
    public void Validate_WithInvalidStartTime_HasStartTimeError(string startTime)
    {
        var result = _validator.Validate(ValidCommand with { Blocks = [new ScheduleBlockInput(DayOfWeek.Monday, startTime, "12:00")] });

        result.Errors.ShouldContain(error => error.PropertyName.EndsWith(nameof(ScheduleBlockInput.StartTime)));
    }

    [Fact]
    public void Validate_WithInvalidEndTime_HasFormatMessage()
    {
        var result = _validator.Validate(ValidCommand with { Blocks = [new ScheduleBlockInput(DayOfWeek.Monday, "09:00", "9:00")] });

        result.Errors.ShouldContain(error =>
            error.PropertyName.EndsWith(nameof(ScheduleBlockInput.EndTime)) && error.ErrorMessage == "Time must use the HH:mm format.");
    }
}
