using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dispensario.Api.IntegrationTests.Common;
using Dispensario.Domain.Users;
using static Dispensario.Api.IntegrationTests.Common.AppointmentRequests;
using static Dispensario.Api.IntegrationTests.Common.DoctorRequests;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Appointments;

public sealed class AppointmentLifecycleEndpointsTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    [Fact]
    public async Task ConfirmAppointment_AsReceptionist_ConfirmsAppointment()
    {
        var reception = await ReceptionAsync();
        var id = await ScheduleNewAppointmentAsync(LocalStart(9, 0));

        var response = await ConfirmAsync(reception, id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var appointment = await GetAppointmentAsync(reception, id);
        appointment.RootElement.GetProperty("status").GetString().ShouldBe("Confirmed");
        appointment.RootElement.GetProperty("cancellationReason").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ConfirmAppointment_Twice_ReturnsBadRequestProblem()
    {
        var reception = await ReceptionAsync();
        var id = await ScheduleNewAppointmentAsync(LocalStart(9, 0));
        (await ConfirmAsync(reception, id)).EnsureSuccessStatusCode();

        var response = await ConfirmAsync(reception, id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task CancelAppointment_AsReceptionist_CancelsAppointmentWithReason()
    {
        var reception = await ReceptionAsync();
        var id = await ScheduleNewAppointmentAsync(LocalStart(9, 0));

        var response = await CancelAsync(reception, id, "Viaje del paciente");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var appointment = await GetAppointmentAsync(reception, id);
        appointment.RootElement.GetProperty("status").GetString().ShouldBe("Cancelled");
        appointment.RootElement.GetProperty("cancellationReason").GetString().ShouldBe("Viaje del paciente");
    }

    [Fact]
    public async Task CancelAppointment_WithoutReason_ReturnsValidationProblem()
    {
        var reception = await ReceptionAsync();
        var id = await ScheduleNewAppointmentAsync(LocalStart(9, 0));

        var response = await CancelAsync(reception, id, "");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("ValidationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CancelAppointment_ThenScheduleSameSlot_CreatesAppointment()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        var id = await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(9, 0));
        (await CancelAsync(admin, id, "Viaje")).EnsureSuccessStatusCode();

        var response = await PostAppointmentAsync(admin, patientId, doctorId, LocalStart(9, 0));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CancelledAppointment_AnyTransition_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();
        var id = await ScheduleNewAppointmentAsync(LocalStart(9, 0));
        (await CancelAsync(admin, id, "Viaje")).EnsureSuccessStatusCode();

        var confirm = await ConfirmAsync(admin, id);
        var cancel = await CancelAsync(admin, id, "Otra vez");
        var noShow = await NoShowAsync(admin, id);
        var reschedule = await RescheduleAsync(admin, id, LocalStart(10, 0));

        confirm.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        cancel.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        noShow.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        reschedule.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RescheduleAppointment_AsReceptionist_MovesAppointmentAndResetsToScheduled()
    {
        var reception = await ReceptionAsync();
        var id = await ScheduleNewAppointmentAsync(LocalStart(9, 0));
        (await ConfirmAsync(reception, id)).EnsureSuccessStatusCode();
        var newStart = LocalStart(10, 0, daysFromNow: 2);

        var response = await RescheduleAsync(reception, id, newStart);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var appointment = await GetAppointmentAsync(reception, id);
        appointment.RootElement.GetProperty("status").GetString().ShouldBe("Scheduled");
        appointment.RootElement.GetProperty("startLocal").GetString().ShouldBe($"{newStart}:00");
        appointment.RootElement.GetProperty("endLocal").GetString().ShouldBe($"{LocalStart(10, 30, daysFromNow: 2)}:00");
    }

    [Fact]
    public async Task RescheduleAppointment_OverlappingDoctorAppointment_ReturnsConflictProblem()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        var id = await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(9, 0));
        var (otherPatientId, _) = await CreatePatientAndDoctorAsync(admin);
        await ScheduleAppointmentAsync(admin, otherPatientId, doctorId, LocalStart(11, 0));

        var response = await RescheduleAsync(admin, id, LocalStart(11, 0));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData("misaligned")]
    [InlineData("utc")]
    [InlineData("same")]
    public async Task RescheduleAppointment_WithInvalidStart_ReturnsBadRequestProblem(string scenario)
    {
        var admin = await AdminAsync();
        var id = await ScheduleNewAppointmentAsync(LocalStart(9, 0));
        var startLocal = scenario switch
        {
            "misaligned" => LocalStart(10, 10),
            "utc" => $"{LocalStart(10, 0)}:00Z",
            _ => LocalStart(9, 0),
        };

        var response = await RescheduleAsync(admin, id, startLocal);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task MarkAppointmentNoShow_BeforeStart_ReturnsBadRequestProblem()
    {
        var reception = await ReceptionAsync();
        var id = await ScheduleNewAppointmentAsync(LocalStart(9, 0));

        var response = await NoShowAsync(reception, id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkAppointmentNoShow_AfterStart_MarksNoShow()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        var id = await SeedStartedAppointmentAsync(factory, patientId, doctorId);
        var reception = await ReceptionAsync();

        var response = await NoShowAsync(reception, id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var appointment = await GetAppointmentAsync(reception, id);
        appointment.RootElement.GetProperty("status").GetString().ShouldBe("NoShow");
    }

    [Fact]
    public async Task LifecycleEndpoints_WithUnknownAppointment_ReturnNotFoundProblem()
    {
        var admin = await AdminAsync();
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await ConfirmAsync(admin, id),
            await CancelAsync(admin, id, "Viaje"),
            await NoShowAsync(admin, id),
            await RescheduleAsync(admin, id, LocalStart(10, 0)),
        };

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, response.RequestMessage?.RequestUri?.ToString());
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    [Fact]
    public async Task LifecycleEndpoints_AsLinkedDoctor_ManageOnlyOwnAppointmentsAndCannotConfirmOrReschedule()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        var (otherPatientId, otherDoctorId) = await CreatePatientAndDoctorAsync(admin);
        var ownFuture = await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(9, 0));
        var ownToConfirm = await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(10, 0));
        var ownStarted = await SeedStartedAppointmentAsync(factory, patientId, doctorId);
        var otherFuture = await ScheduleAppointmentAsync(admin, otherPatientId, otherDoctorId, LocalStart(9, 0));
        var otherStarted = await SeedStartedAppointmentAsync(factory, otherPatientId, otherDoctorId);
        var doctor = await AuthenticateAsLinkedDoctorAsync(factory, admin, doctorId);

        var cancelOwn = await CancelAsync(doctor, ownFuture, "Congreso médico");
        var noShowOwn = await NoShowAsync(doctor, ownStarted);
        var cancelOther = await CancelAsync(doctor, otherFuture, "Congreso médico");
        var noShowOther = await NoShowAsync(doctor, otherStarted);
        var confirm = await ConfirmAsync(doctor, ownToConfirm);
        var reschedule = await RescheduleAsync(doctor, ownToConfirm, LocalStart(11, 0));

        cancelOwn.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        noShowOwn.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        cancelOther.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        noShowOther.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        confirm.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        reschedule.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LifecycleEndpoints_AsUnlinkedDoctor_ReturnForbiddenProblem()
    {
        var doctor = await factory.AuthenticateAsAsync(Role.Doctor, TestContext.Current.CancellationToken);
        var id = await ScheduleNewAppointmentAsync(LocalStart(9, 0));

        var cancel = await CancelAsync(doctor, id, "Viaje");
        var noShow = await NoShowAsync(doctor, id);

        cancel.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        noShow.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        cancel.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task LifecycleEndpoints_WithoutToken_ReturnUnauthorizedProblem()
    {
        var client = factory.CreateClient();
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await ConfirmAsync(client, id),
            await CancelAsync(client, id, "Viaje"),
            await NoShowAsync(client, id),
            await RescheduleAsync(client, id, LocalStart(10, 0)),
        };

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, response.RequestMessage?.RequestUri?.ToString());
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    // Cita nueva de un paciente y un doctor nuevos, agendada por el Admin
    private async Task<Guid> ScheduleNewAppointmentAsync(string startLocal)
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        return await ScheduleAppointmentAsync(admin, patientId, doctorId, startLocal);
    }

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, Guid id) =>
        client.PostAsync($"{AppointmentsRoute}/{id}/confirm", null, TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> CancelAsync(HttpClient client, Guid id, string reason) =>
        client.PostAsJsonAsync($"{AppointmentsRoute}/{id}/cancel", new { reason }, TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> NoShowAsync(HttpClient client, Guid id) =>
        client.PostAsync($"{AppointmentsRoute}/{id}/no-show", null, TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> RescheduleAsync(HttpClient client, Guid id, string startLocal) =>
        client.PutAsJsonAsync($"{AppointmentsRoute}/{id}/schedule", new { startLocal }, TestContext.Current.CancellationToken);

    private static async Task<JsonDocument> GetAppointmentAsync(HttpClient client, Guid id) =>
        await ReadJsonAsync(await client.GetAsync($"{AppointmentsRoute}/{id}", TestContext.Current.CancellationToken));

    private Task<HttpClient> AdminAsync() => factory.AuthenticateAsAsync(Role.Admin, TestContext.Current.CancellationToken);

    private Task<HttpClient> ReceptionAsync() => factory.AuthenticateAsAsync(Role.Receptionist, TestContext.Current.CancellationToken);
}
