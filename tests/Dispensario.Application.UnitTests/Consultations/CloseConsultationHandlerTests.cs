using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Consultations.Commands.CloseConsultation;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Consultations;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Consultations;

public sealed class CloseConsultationHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_AsOwningDoctor_PersistsClosedWithClosedAtFromClock()
    {
        var doctorId = DoctorId.New();
        var consultation = await AddAsync(TestConsultations.Start(doctorId: doctorId));
        ActAsDoctor(doctorId);
        _db.TimeProvider.Advance(TimeSpan.FromMinutes(25));

        await HandleAsync(consultation.Id.Value, "Cefalea tensional", "Control en dos semanas.");

        var loaded = await LoadAsync(consultation.Id);
        loaded.Status.ShouldBe(ConsultationStatus.Closed);
        loaded.Diagnosis.ShouldBe("Cefalea tensional");
        loaded.Notes.ShouldBe("Control en dos semanas.");
        loaded.ClosedAtUtc.ShouldBe(TestConsultations.StartedAtUtc.AddMinutes(25));
    }

    [Fact]
    public async Task Handle_WithUnknownId_ThrowsNotFoundException()
    {
        ActAsDoctor(DoctorId.New());

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Handle_AsOtherDoctor_ThrowsForbiddenException()
    {
        var consultation = await AddAsync(TestConsultations.Start());
        ActAsDoctor(DoctorId.New());

        var exception = await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(consultation.Id.Value));

        exception.Message.ShouldBe("You can only manage your own consultations.");
        (await LoadAsync(consultation.Id)).Status.ShouldBe(ConsultationStatus.Open);
    }

    [Fact]
    public async Task Handle_AsUnlinkedDoctor_ThrowsForbiddenException()
    {
        ActAsDoctor(null);

        var exception = await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(Guid.CreateVersion7()));

        exception.Message.ShouldBe("Your user is not linked to a doctor.");
    }

    [Fact]
    public async Task Handle_WithClosedConsultation_ThrowsDomainException()
    {
        var doctorId = DoctorId.New();
        var consultation = TestConsultations.Start(doctorId: doctorId);
        consultation.Close("Cefalea tensional", null, TestConsultations.StartedAtUtc);
        await AddAsync(consultation);
        ActAsDoctor(doctorId);

        var exception = await Should.ThrowAsync<DomainException>(() => HandleAsync(consultation.Id.Value));

        exception.Message.ShouldBe("Consultation is already closed.");
    }

    public void Dispose() => _db.Dispose();

    private void ActAsDoctor(DoctorId? doctorId)
    {
        _db.CurrentUser.Role.Returns(Role.Doctor);
        _db.CurrentUser.DoctorId.Returns(doctorId);
    }

    private async Task<Consultation> AddAsync(Consultation consultation)
    {
        await using var context = _db.CreateContext();
        context.Consultations.Add(consultation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return consultation;
    }

    private async Task<Consultation> LoadAsync(ConsultationId id)
    {
        await using var context = _db.CreateContext();
        return await context.Consultations.SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task HandleAsync(Guid id, string diagnosis = "Cefalea tensional", string? notes = null)
    {
        await using var context = _db.CreateContext();
        await new CloseConsultationHandler(context, _db.CurrentUser, _db.TimeProvider)
            .Handle(new CloseConsultationCommand(id, diagnosis, notes), TestContext.Current.CancellationToken);
    }
}
