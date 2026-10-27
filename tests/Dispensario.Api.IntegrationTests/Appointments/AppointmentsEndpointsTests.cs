using System.Net;
using System.Net.Http.Json;
using Dispensario.Api.IntegrationTests.Common;
using Dispensario.Domain.Users;
using static Dispensario.Api.IntegrationTests.Common.AppointmentRequests;
using static Dispensario.Api.IntegrationTests.Common.DoctorRequests;
using static Dispensario.Api.IntegrationTests.Common.PatientRequests;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Appointments;

public sealed class AppointmentsEndpointsTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    [Fact]
    public async Task ScheduleAppointment_AsReceptionist_CreatesScheduledAppointment()
    {
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(await AdminAsync());
        var reception = await ReceptionAsync();
        var startLocal = LocalStart(9, 30);

        var response = await PostAppointmentAsync(reception, patientId, doctorId, startLocal);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var created = await ReadJsonAsync(response);
        var id = created.RootElement.GetProperty("id").GetGuid();
        response.Headers.Location?.ToString().ShouldBe($"{AppointmentsRoute}/{id}");

        using var appointment = await ReadJsonAsync(await reception.GetAsync($"{AppointmentsRoute}/{id}", TestContext.Current.CancellationToken));
        var root = appointment.RootElement;
        root.GetProperty("patientId").GetGuid().ShouldBe(patientId);
        root.GetProperty("patientFullName").GetString().ShouldStartWith("Ana Perez-");
        root.GetProperty("doctorId").GetGuid().ShouldBe(doctorId);
        root.GetProperty("doctorFullName").GetString().ShouldBe("Luis Mora");
        root.GetProperty("status").GetString().ShouldBe("Scheduled");
        root.GetProperty("reason").GetString().ShouldBe("Control");
        root.GetProperty("startLocal").GetString().ShouldBe($"{startLocal}:00");
        root.GetProperty("endLocal").GetString().ShouldBe($"{LocalStart(10, 0)}:00");

        // Guayaquil es UTC-5: startUtc = startLocal + 5 h y endUtc = startUtc + 30 min
        var startUtc = root.GetProperty("startUtc").GetDateTime().ToUniversalTime();
        startUtc.ShouldBe(DateTime.Parse(startLocal).AddHours(5));
        root.GetProperty("endUtc").GetDateTime().ToUniversalTime().ShouldBe(startUtc.AddMinutes(30));
        root.GetProperty("startUtc").GetString()!.ShouldEndWith("Z");
        root.GetProperty("createdAtUtc").GetDateTime().ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
    }

    [Fact]
    public async Task ScheduleAppointment_AsAdmin_CreatesAppointment()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);

        var response = await PostAppointmentAsync(admin, patientId, doctorId, LocalStart(10, 0));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ScheduleAppointment_OverlappingDoctorAppointment_ReturnsConflictProblem()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(11, 0));
        var otherPatientId = await CreatePatientAsync(admin);

        var response = await PostAppointmentAsync(admin, otherPatientId, doctorId, LocalStart(11, 0));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task ScheduleAppointment_OverlappingPatientAppointmentWithOtherDoctor_ReturnsConflictProblem()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(11, 0));
        var otherDoctorId = await CreateDoctorAsync(admin);
        await SetFullWeekScheduleAsync(admin, otherDoctorId);

        var response = await PostAppointmentAsync(admin, patientId, otherDoctorId, LocalStart(11, 0));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ScheduleAppointment_ContiguousToOtherAppointment_CreatesAppointment()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(11, 0));

        var response = await PostAppointmentAsync(admin, patientId, doctorId, LocalStart(11, 30));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(19, 0)]    // fuera del horario
    [InlineData(17, 45)]   // sobresale del bloque
    [InlineData(9, 10)]    // desalineada
    public async Task ScheduleAppointment_OutsideScheduleOrMisaligned_ReturnsBadRequestProblem(int hour, int minute)
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);

        var response = await PostAppointmentAsync(admin, patientId, doctorId, LocalStart(hour, minute));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task ScheduleAppointment_WithUtcStart_ReturnsValidationProblem()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);

        var response = await PostAppointmentAsync(admin, patientId, doctorId, $"{LocalStart(9, 0)}:00Z");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("ValidationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ScheduleAppointment_InThePast_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);

        var response = await PostAppointmentAsync(admin, patientId, doctorId, LocalStart(9, 0, daysFromNow: -1));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ScheduleAppointment_WithEmptyReason_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        var body = NewAppointmentBody(patientId, doctorId, LocalStart(9, 0));
        body["reason"] = "";

        var response = await admin.PostAsJsonAsync(AppointmentsRoute, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ScheduleAppointment_WithUnknownPatient_ReturnsNotFoundProblem()
    {
        var admin = await AdminAsync();
        var (_, doctorId) = await CreatePatientAndDoctorAsync(admin);

        var response = await PostAppointmentAsync(admin, Guid.NewGuid(), doctorId, LocalStart(9, 0));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ScheduleAppointment_WithInactiveDoctor_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        (await admin.PostAsync($"{DoctorsRoute}/{doctorId}/deactivate", null, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        var response = await PostAppointmentAsync(admin, patientId, doctorId, LocalStart(9, 0));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ScheduleAppointment_WithInactivePatient_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        (await admin.PostAsync($"{PatientsRoute}/{patientId}/deactivate", null, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        var response = await PostAppointmentAsync(admin, patientId, doctorId, LocalStart(9, 0));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAppointmentById_WithUnknownId_ReturnsNotFoundProblem()
    {
        var reception = await ReceptionAsync();

        var response = await reception.GetAsync($"{AppointmentsRoute}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetAppointments_WithDoctorAndDateRange_ListsAppointmentsOrderedByStart()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        var later = await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(15, 0));
        var earlier = await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(8, 0));
        await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(8, 0, daysFromNow: 2));
        var day = LocalDate().ToString("yyyy-MM-dd");

        var response = await (await ReceptionAsync()).GetAsync(
            $"{AppointmentsRoute}?doctorId={doctorId}&from={day}&to={day}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("pageIndex").GetInt32().ShouldBe(0);
        json.RootElement.GetProperty("pageSize").GetInt32().ShouldBe(10);
        json.RootElement.GetProperty("count").GetInt64().ShouldBe(2);
        json.RootElement.GetProperty("data").EnumerateArray().Select(a => a.GetProperty("id").GetGuid())
            .ShouldBe([earlier, later]);
    }

    [Fact]
    public async Task GetAppointments_WithPatientAndStatus_FiltersAppointments()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        var id = await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(12, 0));

        using var scheduled = await ReadJsonAsync(await admin.GetAsync(
            $"{AppointmentsRoute}?patientId={patientId}&status=Scheduled", TestContext.Current.CancellationToken));
        using var cancelled = await ReadJsonAsync(await admin.GetAsync(
            $"{AppointmentsRoute}?patientId={patientId}&status=Cancelled", TestContext.Current.CancellationToken));

        scheduled.RootElement.GetProperty("data").EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(id);
        cancelled.RootElement.GetProperty("count").GetInt64().ShouldBe(0);
    }

    [Theory]
    [InlineData("from=2026-11-04&to=2026-11-03")]
    [InlineData("pageSize=101")]
    public async Task GetAppointments_WithInvalidQuery_ReturnsBadRequestProblem(string queryString)
    {
        var reception = await ReceptionAsync();

        var response = await reception.GetAsync($"{AppointmentsRoute}?{queryString}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task AppointmentsEndpoints_AsLinkedDoctor_SeeOnlyOwnAppointmentsAndCannotSchedule()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        var (otherPatientId, otherDoctorId) = await CreatePatientAndDoctorAsync(admin);
        var ownId = await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(9, 0));
        var otherId = await ScheduleAppointmentAsync(admin, otherPatientId, otherDoctorId, LocalStart(9, 0));
        var doctor = await AuthenticateAsLinkedDoctorAsync(factory, admin, doctorId);

        var schedule = await PostAppointmentAsync(doctor, patientId, doctorId, LocalStart(10, 0));
        using var list = await ReadJsonAsync(await doctor.GetAsync(AppointmentsRoute, TestContext.Current.CancellationToken));
        var ownDetail = await doctor.GetAsync($"{AppointmentsRoute}/{ownId}", TestContext.Current.CancellationToken);
        var otherDetail = await doctor.GetAsync($"{AppointmentsRoute}/{otherId}", TestContext.Current.CancellationToken);
        var otherFilter = await doctor.GetAsync($"{AppointmentsRoute}?doctorId={otherDoctorId}", TestContext.Current.CancellationToken);

        schedule.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        list.RootElement.GetProperty("data").EnumerateArray()
            .ShouldAllBe(a => a.GetProperty("doctorId").GetGuid() == doctorId);
        list.RootElement.GetProperty("count").GetInt64().ShouldBe(1);
        ownDetail.StatusCode.ShouldBe(HttpStatusCode.OK);
        otherDetail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        otherFilter.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        otherFilter.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetAppointmentEndpoints_AsUnlinkedDoctor_ReturnForbiddenProblem()
    {
        var doctor = await factory.AuthenticateAsAsync(Role.Doctor, TestContext.Current.CancellationToken);

        var list = await doctor.GetAsync(AppointmentsRoute, TestContext.Current.CancellationToken);
        var detail = await doctor.GetAsync($"{AppointmentsRoute}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        list.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        detail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AppointmentsEndpoints_WithoutToken_ReturnUnauthorizedProblem()
    {
        var client = factory.CreateClient();
        var requests = new[]
        {
            new HttpRequestMessage(HttpMethod.Post, AppointmentsRoute)
            {
                Content = JsonContent.Create(NewAppointmentBody(Guid.NewGuid(), Guid.NewGuid(), LocalStart(9, 0)))
            },
            new HttpRequestMessage(HttpMethod.Get, AppointmentsRoute),
            new HttpRequestMessage(HttpMethod.Get, $"{AppointmentsRoute}/{Guid.NewGuid()}"),
        };

        foreach (var request in requests)
        {
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{request.Method} {request.RequestUri}");
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    private Task<HttpClient> AdminAsync() => factory.AuthenticateAsAsync(Role.Admin, TestContext.Current.CancellationToken);

    private Task<HttpClient> ReceptionAsync() => factory.AuthenticateAsAsync(Role.Receptionist, TestContext.Current.CancellationToken);
}
