using BuildingBlocks.Domain;
using BuildingBlocks.Exceptions;
using Dispensario.Application.Patients.Commands.ActivatePatient;
using Dispensario.Application.Patients.Commands.DeactivatePatient;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Patients;

public sealed class DeactivatePatientHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Deactivate_ActivePatient_SetsInactive()
    {
        var patient = await AddAsync(TestPatients.Create());

        await DeactivateAsync(patient.Id);

        (await LoadAsync(patient.Id)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_InactivePatient_ThrowsDomainException()
    {
        var patient = TestPatients.Create();
        patient.Deactivate();
        await AddAsync(patient);

        await Should.ThrowAsync<DomainException>(() => DeactivateAsync(patient.Id));
    }

    [Fact]
    public async Task Deactivate_UnknownPatient_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => DeactivateAsync(PatientId.New()));
    }

    [Fact]
    public async Task Activate_InactivePatient_SetsActive()
    {
        var patient = TestPatients.Create();
        patient.Deactivate();
        await AddAsync(patient);

        await ActivateAsync(patient.Id);

        (await LoadAsync(patient.Id)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Activate_ActivePatient_ThrowsDomainException()
    {
        var patient = await AddAsync(TestPatients.Create());

        await Should.ThrowAsync<DomainException>(() => ActivateAsync(patient.Id));
    }

    [Fact]
    public async Task Activate_UnknownPatient_ThrowsNotFoundException()
    {
        await Should.ThrowAsync<NotFoundException>(() => ActivateAsync(PatientId.New()));
    }

    public void Dispose() => _db.Dispose();

    private async Task<Patient> AddAsync(Patient patient)
    {
        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return patient;
    }

    private async Task<Patient> LoadAsync(PatientId patientId)
    {
        await using var context = _db.CreateContext();
        return await context.Patients.SingleAsync(p => p.Id == patientId, TestContext.Current.CancellationToken);
    }

    private async Task DeactivateAsync(PatientId patientId)
    {
        await using var context = _db.CreateContext();
        await new DeactivatePatientHandler(context)
            .Handle(new DeactivatePatientCommand(patientId.Value), TestContext.Current.CancellationToken);
    }

    private async Task ActivateAsync(PatientId patientId)
    {
        await using var context = _db.CreateContext();
        await new ActivatePatientHandler(context)
            .Handle(new ActivatePatientCommand(patientId.Value), TestContext.Current.CancellationToken);
    }
}
