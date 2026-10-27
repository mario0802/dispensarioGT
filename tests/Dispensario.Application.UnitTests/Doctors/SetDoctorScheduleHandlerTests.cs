using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Doctors.Commands.SetDoctorSchedule;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Doctors;

public sealed class SetDoctorScheduleHandlerTests : IDisposable
{
    private static readonly ScheduleBlockInput[] MondayMorning = [new(DayOfWeek.Monday, "09:00", "12:00")];

    private readonly DispensarioTestDb _db = new();

    public SetDoctorScheduleHandlerTests() => _db.CurrentUser.Role.Returns(Role.Admin);

    [Fact]
    public async Task Handle_AsAdmin_ReplacesScheduleOfAnyDoctor()
    {
        var doctor = await AddAsync(TestDoctors.Create());

        await HandleAsync(new SetDoctorScheduleCommand(
            doctor.Id.Value,
            [new(DayOfWeek.Monday, "09:00", "12:00"), new(DayOfWeek.Friday, "14:30", "18:00")]));

        (await LoadAsync(doctor.Id)).Schedule.ShouldBe(
            [Block(DayOfWeek.Monday, 9, 0, 12, 0), Block(DayOfWeek.Friday, 14, 30, 18, 0)],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Handle_AsOwnDoctor_ReplacesSchedule()
    {
        var doctor = await AddAsync(TestDoctors.Create());
        ActAsDoctor(doctor.Id);

        await HandleAsync(new SetDoctorScheduleCommand(doctor.Id.Value, MondayMorning));

        (await LoadAsync(doctor.Id)).Schedule.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_AsDoctorOnOtherDoctor_ThrowsForbiddenException()
    {
        var other = await AddAsync(TestDoctors.Create(licenseNumber: "MP-111"));
        ActAsDoctor(DoctorId.New());

        await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(new SetDoctorScheduleCommand(other.Id.Value, MondayMorning)));

        (await LoadAsync(other.Id)).Schedule.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_AsDoctorOnUnknownDoctor_ThrowsForbiddenException()
    {
        ActAsDoctor(DoctorId.New());

        await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(new SetDoctorScheduleCommand(Guid.NewGuid(), MondayMorning)));
    }

    [Fact]
    public async Task Handle_AsUnlinkedDoctor_ThrowsForbiddenException()
    {
        var doctor = await AddAsync(TestDoctors.Create());
        _db.CurrentUser.Role.Returns(Role.Doctor);
        _db.CurrentUser.DoctorId.Returns((DoctorId?)null);

        await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(new SetDoctorScheduleCommand(doctor.Id.Value, MondayMorning)));
    }

    [Fact]
    public async Task Handle_WithUnknownDoctor_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(new SetDoctorScheduleCommand(Guid.NewGuid(), MondayMorning)));
    }

    [Fact]
    public async Task Handle_WithOverlappingBlocks_ThrowsDomainException()
    {
        var doctor = await AddAsync(TestDoctors.Create());

        var act = () => HandleAsync(new SetDoctorScheduleCommand(
            doctor.Id.Value,
            [new(DayOfWeek.Monday, "09:00", "12:00"), new(DayOfWeek.Monday, "11:00", "13:00")]));

        (await act.ShouldThrowAsync<DomainException>()).Message.ShouldBe("Schedule blocks must not overlap.");
    }

    [Fact]
    public async Task Handle_WithInactiveDoctor_ThrowsDomainException()
    {
        var doctor = TestDoctors.Create();
        doctor.Deactivate();
        await AddAsync(doctor);

        var act = () => HandleAsync(new SetDoctorScheduleCommand(doctor.Id.Value, MondayMorning));

        (await act.ShouldThrowAsync<DomainException>()).Message.ShouldBe("Doctor is inactive.");
    }

    public void Dispose() => _db.Dispose();

    private void ActAsDoctor(DoctorId doctorId)
    {
        _db.CurrentUser.Role.Returns(Role.Doctor);
        _db.CurrentUser.DoctorId.Returns(doctorId);
    }

    private static ScheduleBlock Block(DayOfWeek day, int startHour, int startMinute, int endHour, int endMinute) =>
        ScheduleBlock.Create(day, new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));

    private async Task<Doctor> AddAsync(Doctor doctor)
    {
        await using var context = _db.CreateContext();
        context.Doctors.Add(doctor);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return doctor;
    }

    private async Task<Doctor> LoadAsync(DoctorId doctorId)
    {
        await using var context = _db.CreateContext();
        return await context.Doctors.SingleAsync(d => d.Id == doctorId, TestContext.Current.CancellationToken);
    }

    private async Task HandleAsync(SetDoctorScheduleCommand command)
    {
        await using var context = _db.CreateContext();
        await new SetDoctorScheduleHandler(context, _db.CurrentUser).Handle(command, TestContext.Current.CancellationToken);
    }
}
