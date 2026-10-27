using BuildingBlocks.Exceptions;
using Dispensario.Application.Doctors.Dtos;
using Dispensario.Application.Doctors.Queries.GetDoctorSchedule;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Doctors;

namespace Dispensario.Application.UnitTests.Doctors;

public sealed class GetDoctorScheduleHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithSchedule_ReturnsBlocksFromMondayToSundayAndByStartTime()
    {
        var doctor = TestDoctors.Create();
        doctor.SetWeeklySchedule(
        [
            Block(DayOfWeek.Sunday, 10, 0, 12, 0),
            Block(DayOfWeek.Monday, 14, 0, 18, 0),
            Block(DayOfWeek.Wednesday, 8, 5, 9, 35),
            Block(DayOfWeek.Monday, 9, 0, 12, 0),
        ]);
        await AddAsync(doctor);

        var result = await HandleAsync(doctor.Id.Value);

        result.DoctorId.ShouldBe(doctor.Id.Value);
        result.Blocks.ShouldBe(
        [
            new ScheduleBlockDto(DayOfWeek.Monday, "09:00", "12:00"),
            new ScheduleBlockDto(DayOfWeek.Monday, "14:00", "18:00"),
            new ScheduleBlockDto(DayOfWeek.Wednesday, "08:05", "09:35"),
            new ScheduleBlockDto(DayOfWeek.Sunday, "10:00", "12:00"),
        ]);
    }

    [Fact]
    public async Task Handle_WithoutSchedule_ReturnsEmptyBlocks()
    {
        var doctor = await AddAsync(TestDoctors.Create());

        var result = await HandleAsync(doctor.Id.Value);

        result.Blocks.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_WithInactiveDoctor_ReturnsSchedule()
    {
        var doctor = TestDoctors.Create();
        doctor.SetWeeklySchedule([Block(DayOfWeek.Monday, 9, 0, 12, 0)]);
        doctor.Deactivate();
        await AddAsync(doctor);

        var result = await HandleAsync(doctor.Id.Value);

        result.Blocks.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_WithUnknownDoctor_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Guid.NewGuid()));
    }

    public void Dispose() => _db.Dispose();

    private static ScheduleBlock Block(DayOfWeek day, int startHour, int startMinute, int endHour, int endMinute) =>
        ScheduleBlock.Create(day, new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));

    private async Task<Doctor> AddAsync(Doctor doctor)
    {
        await using var context = _db.CreateContext();
        context.Doctors.Add(doctor);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return doctor;
    }

    private async Task<DoctorScheduleDto> HandleAsync(Guid doctorId)
    {
        await using var context = _db.CreateContext();
        return await new GetDoctorScheduleHandler(context).Handle(new GetDoctorScheduleQuery(doctorId), TestContext.Current.CancellationToken);
    }
}
