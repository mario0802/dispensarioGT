using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Consultations.Commands.UpdateConsultation;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Consultations;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Consultations;

public sealed class UpdateConsultationHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_AsOwningDoctor_PersistsTexts()
    {
        var doctorId = DoctorId.New();
        var consultation = await AddAsync(TestConsultations.Start(doctorId: doctorId));
        ActAsDoctor(doctorId);

        await HandleAsync(consultation.Id.Value, "Cefalea", "Cefalea tensional", "Sin signos de alarma.");

        var loaded = await LoadAsync(consultation.Id);
        loaded.Reason.ShouldBe("Cefalea");
        loaded.Diagnosis.ShouldBe("Cefalea tensional");
        loaded.Notes.ShouldBe("Sin signos de alarma.");
        loaded.Status.ShouldBe(ConsultationStatus.Open);
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
        (await LoadAsync(consultation.Id)).Reason.ShouldBe("Dolor de cabeza");
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

        exception.Message.ShouldBe("Only open consultations can be updated.");
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

    private async Task HandleAsync(Guid id, string reason = "Otro motivo", string? diagnosis = null, string? notes = null)
    {
        await using var context = _db.CreateContext();
        await new UpdateConsultationHandler(context, _db.CurrentUser)
            .Handle(new UpdateConsultationCommand(id, reason, diagnosis, notes), TestContext.Current.CancellationToken);
    }
}
