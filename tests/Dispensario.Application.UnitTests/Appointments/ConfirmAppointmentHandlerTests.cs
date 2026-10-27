using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Appointments.Commands.ConfirmAppointment;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Appointments;

public sealed class ConfirmAppointmentHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithScheduledAppointment_PersistsConfirmed()
    {
        var appointment = await AddAsync(TestAppointments.Create());

        await HandleAsync(appointment.Id.Value);

        (await LoadAsync(appointment.Id)).Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task Handle_WithUnknownId_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Handle_WithConfirmedAppointment_ThrowsDomainException()
    {
        var appointment = TestAppointments.Create();
        appointment.Confirm(TestAppointments.NowUtc);
        await AddAsync(appointment);

        var exception = await Should.ThrowAsync<DomainException>(() => HandleAsync(appointment.Id.Value));

        exception.Message.ShouldBe("Only scheduled appointments can be confirmed.");
    }

    public void Dispose() => _db.Dispose();

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
        await new ConfirmAppointmentHandler(context, _db.TimeProvider)
            .Handle(new ConfirmAppointmentCommand(id), TestContext.Current.CancellationToken);
    }
}
