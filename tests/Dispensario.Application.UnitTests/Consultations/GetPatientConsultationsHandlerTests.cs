using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using Dispensario.Application.Consultations.Dtos;
using Dispensario.Application.Consultations.Queries.GetPatientConsultations;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Consultations;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Dispensario.Domain.Users;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Consultations;

public sealed class GetPatientConsultationsHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    [Fact]
    public async Task Handle_WithConsultationsOfSeveralDoctors_ReturnsAllNewestFirst()
    {
        var patient = await AddPatientAsync(TestPatients.Create());
        var luis = await AddDoctorAsync(TestDoctors.Create());
        var marta = await AddDoctorAsync(TestDoctors.Create(licenseNumber: "MP-456", firstName: "Marta", lastName: "Ruiz", email: "marta@dispensario.local"));
        var oldest = TestConsultations.Start(patient.Id, luis.Id, startedAtUtc: TestConsultations.StartedAtUtc.AddDays(-10));
        oldest.Close("Gripe", null, TestConsultations.StartedAtUtc.AddDays(-10).AddMinutes(20));
        var middle = TestConsultations.Start(patient.Id, marta.Id, startedAtUtc: TestConsultations.StartedAtUtc.AddDays(-2));
        var newest = TestConsultations.Start(patient.Id, luis.Id);
        await AddAsync(middle, oldest, newest, TestConsultations.Start(PatientId.New(), luis.Id));
        _db.CurrentUser.Role.Returns(Role.Admin);

        var result = await HandleAsync(patient.Id.Value);

        var data = result.Data.ToList();
        result.Count.ShouldBe(3);
        data.Select(c => c.Id).ShouldBe([newest.Id.Value, middle.Id.Value, oldest.Id.Value]);
        data.Select(c => c.DoctorFullName).ShouldBe(["Luis Mora", "Marta Ruiz", "Luis Mora"]);
        data.Select(c => c.Status).ShouldBe([ConsultationStatus.Open, ConsultationStatus.Open, ConsultationStatus.Closed]);
        data[0].PatientFullName.ShouldBe("Ana Pérez");
        data[0].StartedAtLocal.ShouldBe(new DateTime(2026, 10, 26, 7, 0, 0));
        data[2].ClosedAtLocal.ShouldBe(new DateTime(2026, 10, 16, 7, 20, 0));
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsRequestedPage()
    {
        var patient = await AddPatientAsync(TestPatients.Create());
        var consultations = Enumerable.Range(0, 3)
            .Select(i => TestConsultations.Start(patient.Id, startedAtUtc: TestConsultations.StartedAtUtc.AddDays(-i)))
            .ToArray();
        await AddAsync(consultations);
        ActAsDoctor(DoctorId.New());

        var result = await HandleAsync(patient.Id.Value, pageIndex: 1, pageSize: 2);

        result.PageIndex.ShouldBe(1);
        result.PageSize.ShouldBe(2);
        result.Count.ShouldBe(3);
        result.Data.Select(c => c.Id).ShouldBe([consultations[2].Id.Value]);
    }

    [Fact]
    public async Task Handle_WithInactivePatient_ReturnsHistory()
    {
        var patient = TestPatients.Create();
        patient.Deactivate();
        await AddPatientAsync(patient);
        await AddAsync(TestConsultations.Start(patient.Id));
        _db.CurrentUser.Role.Returns(Role.Admin);

        var result = await HandleAsync(patient.Id.Value);

        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_WithoutConsultations_ReturnsEmptyPage()
    {
        var patient = await AddPatientAsync(TestPatients.Create());
        _db.CurrentUser.Role.Returns(Role.Admin);

        var result = await HandleAsync(patient.Id.Value);

        result.Count.ShouldBe(0);
        result.Data.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_WithUnknownPatient_ThrowsNotFoundException()
    {
        _db.CurrentUser.Role.Returns(Role.Admin);

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Guid.CreateVersion7()));
    }

    [Theory]
    [InlineData(-1, 10, "PageIndex must be greater than or equal to 0.")]
    [InlineData(0, 0, "PageSize must be between 1 and 100.")]
    [InlineData(0, 101, "PageSize must be between 1 and 100.")]
    public async Task Handle_WithInvalidPagination_ThrowsBadRequestExceptionBeforeReadingPatient(int pageIndex, int pageSize, string message)
    {
        _db.CurrentUser.Role.Returns(Role.Admin);

        var exception = await Should.ThrowAsync<BadRequestException>(
            () => HandleAsync(Guid.CreateVersion7(), pageIndex, pageSize));

        exception.Message.ShouldBe(message);
    }

    [Fact]
    public async Task Handle_AsUnlinkedDoctor_ThrowsForbiddenExceptionBeforeReading()
    {
        ActAsDoctor(null);

        var exception = await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(Guid.CreateVersion7(), pageSize: 0));

        exception.Message.ShouldBe("Your user is not linked to a doctor.");
    }

    public void Dispose() => _db.Dispose();

    private void ActAsDoctor(DoctorId? doctorId)
    {
        _db.CurrentUser.Role.Returns(Role.Doctor);
        _db.CurrentUser.DoctorId.Returns(doctorId);
    }

    private async Task<Patient> AddPatientAsync(Patient patient)
    {
        await using var context = _db.CreateContext();
        context.Patients.Add(patient);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return patient;
    }

    private async Task<Doctor> AddDoctorAsync(Doctor doctor)
    {
        await using var context = _db.CreateContext();
        context.Doctors.Add(doctor);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return doctor;
    }

    private async Task AddAsync(params Consultation[] consultations)
    {
        await using var context = _db.CreateContext();
        context.Consultations.AddRange(consultations);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<PaginatedResult<ConsultationDto>> HandleAsync(Guid patientId, int pageIndex = 0, int pageSize = 10)
    {
        await using var context = _db.CreateContext();
        return await new GetPatientConsultationsHandler(context, _db.CurrentUser, _db.ClinicTimeZone)
            .Handle(
                new GetPatientConsultationsQuery(patientId, new PaginationRequest(pageIndex, pageSize)),
                TestContext.Current.CancellationToken);
    }
}
