using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Appointments.Commands.MarkAppointmentNoShow;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Appointments;

public sealed class MarkAppointmentNoShowHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_AfterStart_PersistsNoShow()
    {
        var appointment = await AddAsync(TestAppointments.Create());
        AdvanceClockPastStart();

        await HandleAsync(appointment.Id.Value);

        (await LoadAsync(appointment.Id)).Status.ShouldBe(AppointmentStatus.NoShow);
    }

    [Fact]
    public async Task Handle_BeforeStart_ThrowsDomainException()
    {
        var appointment = await AddAsync(TestAppointments.Create());

        var exception = await Should.ThrowAsync<DomainException>(() => HandleAsync(appointment.Id.Value));

        exception.Message.ShouldBe("Appointment has not started yet.");
    }

    [Fact]
    public async Task Handle_WithUnknownId_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Handle_AsDoctorOwningAppointment_PersistsNoShow()
    {
        var doctorId = DoctorId.New();
        var appointment = await AddAsync(TestAppointments.Create(doctorId: doctorId));
        AdvanceClockPastStart();
        ActAsDoctor(doctorId);

        await HandleAsync(appointment.Id.Value);

        (await LoadAsync(appointment.Id)).Status.ShouldBe(AppointmentStatus.NoShow);
    }

    [Fact]
    public async Task Handle_AsOtherDoctor_ThrowsForbiddenException()
    {
        var appointment = await AddAsync(TestAppointments.Create());
        AdvanceClockPastStart();
        ActAsDoctor(DoctorId.New());

        var exception = await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(appointment.Id.Value));

        exception.Message.ShouldBe("You can only manage your own appointments.");
    }

    [Fact]
    public async Task Handle_AsUnlinkedDoctor_ThrowsForbiddenException()
    {
        var appointment = await AddAsync(TestAppointments.Create());
        ActAsDoctor(null);

        var exception = await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(appointment.Id.Value));

        exception.Message.ShouldBe("Your user is not linked to a doctor.");
    }

    public void Dispose() => _db.Dispose();

    // Lleva el reloj falso a 10 minutos después del inicio de la cita por defecto
    private void AdvanceClockPastStart() =>
        _db.TimeProvider.SetUtcNow(new DateTimeOffset(TestAppointments.DefaultStartUtc.AddMinutes(10)));

    private void ActAsDoctor(DoctorId? doctorId)
    {
        _db.CurrentUser.Role.Returns(Role.Doctor);
        _db.CurrentUser.DoctorId.Returns(doctorId);
    }

    private async Task<Appointment> AddAsync(Appointment appointment)
    {
        await using var context = _db.CreateContext();
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return appointment;
    }

    private async Task<Appointment> LoadAsync(AppointmentId id)
    {
        await using var context = _db.CreateContext();
        return await context.Appointments.SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task HandleAsync(Guid id)
    {
        await using var context = _db.CreateContext();
        await new MarkAppointmentNoShowHandler(context, _db.CurrentUser, _db.TimeProvider)
            .Handle(new MarkAppointmentNoShowCommand(id), TestContext.Current.CancellationToken);
    }
}
