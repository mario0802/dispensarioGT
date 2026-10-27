using Dispensario.Application.Appointments.EventHandlers;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Consultations;
using Dispensario.Domain.Consultations.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dispensario.Application.UnitTests.Appointments;

public sealed class ConsultationClosedHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithScheduledAppointment_CompletesAppointment()
    {
        var appointment = await AddAsync(TestAppointments.Create());

        await HandleAsync(appointment.Id);

        (await LoadAsync(appointment.Id)).Status.ShouldBe(AppointmentStatus.Completed);
    }

    [Fact]
    public async Task Handle_WithConfirmedAppointment_CompletesAppointment()
    {
        var appointment = TestAppointments.Create();
        appointment.Confirm(TestAppointments.NowUtc);
        await AddAsync(appointment);

        await HandleAsync(appointment.Id);

        (await LoadAsync(appointment.Id)).Status.ShouldBe(AppointmentStatus.Completed);
    }

    [Fact]
    public async Task Handle_WithoutAppointment_DoesNothing()
    {
        var appointment = await AddAsync(TestAppointments.Create());

        await HandleAsync(appointmentId: null);

        (await LoadAsync(appointment.Id)).Status.ShouldBe(AppointmentStatus.Scheduled);
    }

    [Fact]
    public async Task Handle_WithUnknownAppointment_DoesNotThrow()
    {
        await Should.NotThrowAsync(() => HandleAsync(AppointmentId.New()));
    }

    [Fact]
    public async Task Handle_WithCancelledAppointment_KeepsStatus()
    {
        var appointment = TestAppointments.Create();
        appointment.Cancel("El paciente no puede asistir", TestAppointments.NowUtc);
        await AddAsync(appointment);

        await HandleAsync(appointment.Id);

        (await LoadAsync(appointment.Id)).Status.ShouldBe(AppointmentStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_WithNoShowAppointment_KeepsStatus()
    {
        var appointment = TestAppointments.Create();
        appointment.MarkNoShow(TestAppointments.DefaultStartUtc);
        await AddAsync(appointment);

        await HandleAsync(appointment.Id);

        (await LoadAsync(appointment.Id)).Status.ShouldBe(AppointmentStatus.NoShow);
    }

    [Fact]
    public async Task Handle_WithCompletedAppointment_KeepsStatus()
    {
        var appointment = TestAppointments.Create();
        appointment.Complete();
        await AddAsync(appointment);

        await Should.NotThrowAsync(() => HandleAsync(appointment.Id));

        (await LoadAsync(appointment.Id)).Status.ShouldBe(AppointmentStatus.Completed);
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

    private async Task HandleAsync(AppointmentId? appointmentId)
    {
        await using var context = _db.CreateContext();
        await new ConsultationClosedHandler(context, NullLogger<ConsultationClosedHandler>.Instance)
            .Handle(
                new ConsultationClosedDomainEvent(ConsultationId.New(), appointmentId),
                TestContext.Current.CancellationToken);
    }
}
