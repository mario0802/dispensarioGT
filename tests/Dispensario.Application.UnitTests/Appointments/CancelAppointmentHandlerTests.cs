using BuildingBlocks.Exceptions;
using Dispensario.Application.Appointments.Commands.CancelAppointment;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Appointments;

public sealed class CancelAppointmentHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_AsReceptionist_PersistsCancelledWithReason()
    {
        var appointment = await AddAsync(TestAppointments.Create());
        _db.CurrentUser.Role.Returns(Role.Receptionist);

        await HandleAsync(appointment.Id.Value, "  Viaje del paciente  ");

        var loaded = await LoadAsync(appointment.Id);
        loaded.Status.ShouldBe(AppointmentStatus.Cancelled);
        loaded.CancellationReason.ShouldBe("Viaje del paciente");
    }

    [Fact]
    public async Task Handle_WithUnknownId_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Handle_AsDoctorOwningAppointment_PersistsCancelled()
    {
        var doctorId = DoctorId.New();
        var appointment = await AddAsync(TestAppointments.Create(doctorId: doctorId));
        ActAsDoctor(doctorId);

        await HandleAsync(appointment.Id.Value);

        (await LoadAsync(appointment.Id)).Status.ShouldBe(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_AsOtherDoctor_ThrowsForbiddenException()
    {
        var appointment = await AddAsync(TestAppointments.Create());
        ActAsDoctor(DoctorId.New());

        var exception = await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(appointment.Id.Value));

        exception.Message.ShouldBe("You can only manage your own appointments.");
        (await LoadAsync(appointment.Id)).Status.ShouldBe(AppointmentStatus.Scheduled);
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

    private async Task HandleAsync(Guid id, string reason = "Viaje")
    {
        await using var context = _db.CreateContext();
        await new CancelAppointmentHandler(context, _db.CurrentUser, _db.TimeProvider)
            .Handle(new CancelAppointmentCommand(id, reason), TestContext.Current.CancellationToken);
    }
}
