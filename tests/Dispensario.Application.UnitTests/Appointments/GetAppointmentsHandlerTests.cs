using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using Dispensario.Application.Appointments.Dtos;
using Dispensario.Application.Appointments.Queries.GetAppointments;
using Dispensario.Application.UnitTests.Common;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Dispensario.Domain.Users;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Appointments;

// Zona por defecto America/Guayaquil (UTC-5)
public sealed class GetAppointmentsHandlerTests : IDisposable
{
    private readonly DispensarioTestDb _db = new();

    private readonly Patient _ana = TestPatients.Create(documentNumber: "0102030405", firstName: "Ana", lastName: "Pérez");
    private readonly Patient _bruno = TestPatients.Create(documentNumber: "0605040302", firstName: "Bruno", lastName: "Díaz");
    private readonly Doctor _luis = TestDoctors.Create(licenseNumber: "MP-1", firstName: "Luis", lastName: "Mora");
    private readonly Doctor _sara = TestDoctors.Create(licenseNumber: "MP-2", firstName: "Sara", lastName: "Vega");

    [Fact]
    public async Task Handle_WithoutFilters_ReturnsAllOrderedByStartWithNamesAndLocalTimes()
    {
        await SeedAsync(
            TestAppointments.Create(_bruno.Id, _sara.Id, Utc(4, 15, 0)),
            TestAppointments.Create(_ana.Id, _luis.Id, Utc(3, 14, 30)));

        var result = await HandleAsync(NewQuery());

        result.Count.ShouldBe(2);
        var data = result.Data.ToList();
        data[0].PatientFullName.ShouldBe("Ana Pérez");
        data[0].DoctorFullName.ShouldBe("Luis Mora");
        data[0].StartLocal.ShouldBe(new DateTime(2026, 11, 3, 9, 30, 0));
        data[1].PatientFullName.ShouldBe("Bruno Díaz");
        data[1].StartLocal.ShouldBe(new DateTime(2026, 11, 4, 10, 0, 0));
    }

    [Fact]
    public async Task Handle_WithPageSize_ReturnsTotalCountAndPage()
    {
        await SeedAsync(
            TestAppointments.Create(_ana.Id, _luis.Id, Utc(3, 14, 0)),
            TestAppointments.Create(_ana.Id, _luis.Id, Utc(3, 15, 0)),
            TestAppointments.Create(_ana.Id, _luis.Id, Utc(3, 16, 0)));

        var result = await HandleAsync(NewQuery() with { Pagination = new PaginationRequest(1, 2) });

        result.Count.ShouldBe(3);
        result.Data.Single().StartUtc.ShouldBe(Utc(3, 16, 0));
    }

    [Fact]
    public async Task Handle_WithDoctorAndPatientFilters_ReturnsMatchingAppointments()
    {
        await SeedAsync(
            TestAppointments.Create(_ana.Id, _luis.Id, Utc(3, 14, 0)),
            TestAppointments.Create(_bruno.Id, _luis.Id, Utc(3, 15, 0)),
            TestAppointments.Create(_ana.Id, _sara.Id, Utc(3, 16, 0)));

        var byDoctor = await HandleAsync(NewQuery() with { DoctorId = _luis.Id.Value });
        var byPatient = await HandleAsync(NewQuery() with { PatientId = _ana.Id.Value });
        var byBoth = await HandleAsync(NewQuery() with { DoctorId = _luis.Id.Value, PatientId = _ana.Id.Value });

        byDoctor.Count.ShouldBe(2);
        byPatient.Count.ShouldBe(2);
        byBoth.Data.Single().StartUtc.ShouldBe(Utc(3, 14, 0));
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsMatchingAppointments()
    {
        await SeedAsync(TestAppointments.Create(_ana.Id, _luis.Id, Utc(3, 14, 0)));

        var scheduled = await HandleAsync(NewQuery() with { Status = AppointmentStatus.Scheduled });
        var cancelled = await HandleAsync(NewQuery() with { Status = AppointmentStatus.Cancelled });

        scheduled.Count.ShouldBe(1);
        cancelled.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_WithLocalDateRange_UsesClinicLocalDates()
    {
        // 20:00 locales del 3 de noviembre son la 01:00 UTC del 4: la cita es del día local 3
        await SeedAsync(
            TestAppointments.Create(_ana.Id, _luis.Id, Utc(4, 1, 0)),
            TestAppointments.Create(_ana.Id, _luis.Id, Utc(4, 14, 0)));

        var onThird = await HandleAsync(NewQuery() with { From = new DateOnly(2026, 11, 3), To = new DateOnly(2026, 11, 3) });
        var fromFourth = await HandleAsync(NewQuery() with { From = new DateOnly(2026, 11, 4) });

        onThird.Data.Single().StartLocal.ShouldBe(new DateTime(2026, 11, 3, 20, 0, 0));
        fromFourth.Data.Single().StartUtc.ShouldBe(Utc(4, 14, 0));
    }

    [Fact]
    public async Task Handle_WithFromAfterTo_ThrowsBadRequestException()
    {
        var query = NewQuery() with { From = new DateOnly(2026, 11, 4), To = new DateOnly(2026, 11, 3) };

        var exception = await Should.ThrowAsync<BadRequestException>(() => HandleAsync(query));

        exception.Message.ShouldBe("From must be on or before To.");
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task Handle_WithInvalidPagination_ThrowsBadRequestException(int pageIndex, int pageSize)
    {
        var query = NewQuery() with { Pagination = new PaginationRequest(pageIndex, pageSize) };

        await Should.ThrowAsync<BadRequestException>(() => HandleAsync(query));
    }

    [Fact]
    public async Task Handle_AsDoctorWithoutFilter_ReturnsOnlyOwnAppointments()
    {
        await SeedAsync(
            TestAppointments.Create(_ana.Id, _luis.Id, Utc(3, 14, 0)),
            TestAppointments.Create(_bruno.Id, _sara.Id, Utc(3, 15, 0)));
        ActAsDoctor(_luis.Id);

        var result = await HandleAsync(NewQuery());

        result.Data.Single().DoctorId.ShouldBe(_luis.Id.Value);
    }

    [Fact]
    public async Task Handle_AsDoctorFilteringByOwnId_ReturnsOwnAppointments()
    {
        await SeedAsync(TestAppointments.Create(_ana.Id, _luis.Id, Utc(3, 14, 0)));
        ActAsDoctor(_luis.Id);

        var result = await HandleAsync(NewQuery() with { DoctorId = _luis.Id.Value });

        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_AsDoctorFilteringByOtherDoctor_ThrowsForbiddenException()
    {
        ActAsDoctor(_luis.Id);

        var exception = await Should.ThrowAsync<ForbiddenException>(
            () => HandleAsync(NewQuery() with { DoctorId = _sara.Id.Value }));

        exception.Message.ShouldBe("You can only view your own appointments.");
    }

    [Fact]
    public async Task Handle_AsUnlinkedDoctor_ThrowsForbiddenException()
    {
        ActAsDoctor(null);

        var exception = await Should.ThrowAsync<ForbiddenException>(() => HandleAsync(NewQuery()));

        exception.Message.ShouldBe("Your user is not linked to a doctor.");
    }

    public void Dispose() => _db.Dispose();

    private static DateTime Utc(int day, int hour, int minute) => new(2026, 11, day, hour, minute, 0, DateTimeKind.Utc);

    private static GetAppointmentsQuery NewQuery() => new(new PaginationRequest(0, 10), null, null, null, null, null);

    private void ActAsDoctor(DoctorId? doctorId)
    {
        _db.CurrentUser.Role.Returns(Role.Doctor);
        _db.CurrentUser.DoctorId.Returns(doctorId);
    }

    private async Task SeedAsync(params Appointment[] appointments)
    {
        await using var context = _db.CreateContext();
        context.Patients.AddRange(_ana, _bruno);
        context.Doctors.AddRange(_luis, _sara);
        context.Appointments.AddRange(appointments);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<PaginatedResult<AppointmentDto>> HandleAsync(GetAppointmentsQuery query)
    {
        await using var context = _db.CreateContext();
        return await new GetAppointmentsHandler(context, _db.CurrentUser, _db.ClinicTimeZone)
            .Handle(query, TestContext.Current.CancellationToken);
    }
}
