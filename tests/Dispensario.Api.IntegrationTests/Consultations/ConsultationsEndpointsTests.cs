using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dispensario.Api.IntegrationTests.Common;
using Dispensario.Domain.Users;
using static Dispensario.Api.IntegrationTests.Common.AppointmentRequests;
using static Dispensario.Api.IntegrationTests.Common.ConsultationRequests;
using static Dispensario.Api.IntegrationTests.Common.DoctorRequests;
using static Dispensario.Api.IntegrationTests.Common.PatientRequests;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Consultations;

public sealed class ConsultationsEndpointsTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    [Fact]
    public async Task StartConsultation_WithoutAppointment_ReturnsCreatedOpenConsultation()
    {
        var (_, doctorClient, doctorId, patientId) = await LinkedDoctorAsync();

        var response = await PostConsultationAsync(doctorClient, NewConsultationBody(patientId));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var created = await ReadJsonAsync(response);
        var id = created.RootElement.GetProperty("id").GetGuid();
        response.Headers.Location?.ToString().ShouldBe($"{ConsultationsRoute}/{id}");

        using var consultation = await ReadJsonAsync(await GetConsultationAsync(doctorClient, id));
        consultation.RootElement.GetProperty("status").GetString().ShouldBe("Open");
        consultation.RootElement.GetProperty("doctorId").GetGuid().ShouldBe(doctorId);
        consultation.RootElement.GetProperty("patientId").GetGuid().ShouldBe(patientId);
        consultation.RootElement.GetProperty("appointmentId").ValueKind.ShouldBe(JsonValueKind.Null);
        consultation.RootElement.GetProperty("doctorFullName").GetString().ShouldBe("Luis Mora");
        consultation.RootElement.GetProperty("closedAtUtc").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task StartConsultation_WithAppointmentOfToday_ReturnsCreatedAndKeepsAppointmentScheduled()
    {
        var (admin, doctorClient, doctorId, patientId) = await LinkedDoctorAsync();
        var appointmentId = await SeedAppointmentStartingNowAsync(factory, patientId, doctorId);

        var response = await PostConsultationAsync(doctorClient, NewConsultationBody(patientId, appointmentId));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var created = await ReadJsonAsync(response);
        using var consultation = await ReadJsonAsync(
            await GetConsultationAsync(doctorClient, created.RootElement.GetProperty("id").GetGuid()));
        consultation.RootElement.GetProperty("appointmentId").GetGuid().ShouldBe(appointmentId);
        using var appointment = await ReadJsonAsync(
            await admin.GetAsync($"{AppointmentsRoute}/{appointmentId}", TestContext.Current.CancellationToken));
        appointment.RootElement.GetProperty("status").GetString().ShouldBe("Scheduled");
    }

    [Fact]
    public async Task StartConsultation_WithAppointmentThatAlreadyHasConsultation_ReturnsConflictProblem()
    {
        var (_, doctorClient, doctorId, patientId) = await LinkedDoctorAsync();
        var appointmentId = await SeedAppointmentStartingNowAsync(factory, patientId, doctorId);
        await StartConsultationAsync(doctorClient, patientId, appointmentId);

        var response = await PostConsultationAsync(doctorClient, NewConsultationBody(patientId, appointmentId));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task StartConsultation_WithAppointmentOfTomorrow_ReturnsBadRequestProblem()
    {
        var (admin, doctorClient, doctorId, patientId) = await LinkedDoctorAsync();
        var appointmentId = await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(9, 0));

        var response = await PostConsultationAsync(doctorClient, NewConsultationBody(patientId, appointmentId));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task StartConsultation_WithAppointmentOfOtherDoctor_ReturnsForbiddenProblem()
    {
        var (admin, doctorClient, _, patientId) = await LinkedDoctorAsync();
        var otherDoctorId = await CreateDoctorAsync(admin);
        var appointmentId = await SeedAppointmentStartingNowAsync(factory, patientId, otherDoctorId);

        var response = await PostConsultationAsync(doctorClient, NewConsultationBody(patientId, appointmentId));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task StartConsultation_WithUnknownPatient_ReturnsNotFoundProblem()
    {
        var (_, doctorClient, _, _) = await LinkedDoctorAsync();

        var response = await PostConsultationAsync(doctorClient, NewConsultationBody(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task StartConsultation_WithInactivePatient_ReturnsBadRequestProblem()
    {
        var (admin, doctorClient, _, patientId) = await LinkedDoctorAsync();
        (await admin.PostAsync($"{PatientsRoute}/{patientId}/deactivate", null, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        var response = await PostConsultationAsync(doctorClient, NewConsultationBody(patientId));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task StartConsultation_WithoutReason_ReturnsValidationProblem()
    {
        var (_, doctorClient, _, patientId) = await LinkedDoctorAsync();
        var body = NewConsultationBody(patientId);
        body["reason"] = "";

        var response = await PostConsultationAsync(doctorClient, body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("ValidationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateConsultation_AsOwningDoctor_ReplacesTexts()
    {
        var (_, doctorClient, _, patientId) = await LinkedDoctorAsync();
        var id = await StartConsultationAsync(doctorClient, patientId);
        (await UpdateConsultationAsync(doctorClient, id, "Cefalea", "Cefalea tensional", "Notas")).EnsureSuccessStatusCode();

        var response = await UpdateConsultationAsync(doctorClient, id, "Cefalea intensa", "Migraña", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var consultation = await ReadJsonAsync(await GetConsultationAsync(doctorClient, id));
        consultation.RootElement.GetProperty("reason").GetString().ShouldBe("Cefalea intensa");
        consultation.RootElement.GetProperty("diagnosis").GetString().ShouldBe("Migraña");
        consultation.RootElement.GetProperty("notes").ValueKind.ShouldBe(JsonValueKind.Null);
        consultation.RootElement.GetProperty("status").GetString().ShouldBe("Open");
    }

    [Fact]
    public async Task CloseConsultation_AsOwningDoctor_ClosesConsultation()
    {
        var (_, doctorClient, _, patientId) = await LinkedDoctorAsync();
        var id = await StartConsultationAsync(doctorClient, patientId);

        var response = await CloseConsultationAsync(doctorClient, id, "Cefalea tensional", "Control en dos semanas.");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var consultation = await ReadJsonAsync(await GetConsultationAsync(doctorClient, id));
        consultation.RootElement.GetProperty("status").GetString().ShouldBe("Closed");
        consultation.RootElement.GetProperty("diagnosis").GetString().ShouldBe("Cefalea tensional");
        consultation.RootElement.GetProperty("notes").GetString().ShouldBe("Control en dos semanas.");
        consultation.RootElement.GetProperty("closedAtUtc").ValueKind.ShouldBe(JsonValueKind.String);
        consultation.RootElement.GetProperty("closedAtLocal").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task CloseConsultation_WithoutDiagnosis_ReturnsValidationProblem()
    {
        var (_, doctorClient, _, patientId) = await LinkedDoctorAsync();
        var id = await StartConsultationAsync(doctorClient, patientId);

        var response = await CloseConsultationAsync(doctorClient, id, "");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("ValidationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateAndClose_OnClosedConsultation_ReturnBadRequestProblem()
    {
        var (_, doctorClient, _, patientId) = await LinkedDoctorAsync();
        var id = await StartConsultationAsync(doctorClient, patientId);
        (await CloseConsultationAsync(doctorClient, id, "Cefalea tensional")).EnsureSuccessStatusCode();

        var responses = new[]
        {
            await UpdateConsultationAsync(doctorClient, id, "Otro motivo", null, null),
            await CloseConsultationAsync(doctorClient, id, "Otro diagnóstico"),
        };

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, response.RequestMessage?.RequestUri?.ToString());
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    [Fact]
    public async Task UpdateAndClose_OnConsultationOfOtherDoctor_ReturnForbiddenProblem()
    {
        var (admin, doctorClient, _, patientId) = await LinkedDoctorAsync();
        var id = await StartConsultationAsync(doctorClient, patientId);
        var otherDoctorClient = await AuthenticateAsLinkedDoctorAsync(factory, admin, await CreateDoctorAsync(admin));

        var responses = new[]
        {
            await UpdateConsultationAsync(otherDoctorClient, id, "Otro motivo", null, null),
            await CloseConsultationAsync(otherDoctorClient, id, "Otro diagnóstico"),
        };

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, response.RequestMessage?.RequestUri?.ToString());
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    [Fact]
    public async Task ReadEndpoints_AsOtherLinkedDoctorAndAsAdmin_ReturnOk()
    {
        var (admin, doctorClient, _, patientId) = await LinkedDoctorAsync();
        var id = await StartConsultationAsync(doctorClient, patientId);
        var otherDoctorClient = await AuthenticateAsLinkedDoctorAsync(factory, admin, await CreateDoctorAsync(admin));

        foreach (var client in new[] { otherDoctorClient, admin })
        {
            (await GetConsultationAsync(client, id)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await GetPatientConsultationsAsync(client, patientId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task GetPatientConsultations_WithSeveralConsultations_ReturnsNewestFirstAndPaginated()
    {
        var (admin, doctorClient, _, patientId) = await LinkedDoctorAsync();
        var otherDoctorClient = await AuthenticateAsLinkedDoctorAsync(factory, admin, await CreateDoctorAsync(admin));
        var first = await StartConsultationAsync(doctorClient, patientId);
        (await CloseConsultationAsync(doctorClient, first, "Cefalea tensional")).EnsureSuccessStatusCode();
        var second = await StartConsultationAsync(otherDoctorClient, patientId);
        var third = await StartConsultationAsync(doctorClient, patientId);

        using var page = await ReadJsonAsync(await GetPatientConsultationsAsync(admin, patientId));
        using var secondPage = await ReadJsonAsync(await GetPatientConsultationsAsync(admin, patientId, "?pageIndex=1&pageSize=2"));

        page.RootElement.GetProperty("count").GetInt64().ShouldBe(3);
        page.RootElement.GetProperty("data").EnumerateArray().Select(c => c.GetProperty("id").GetGuid())
            .ShouldBe([third, second, first]);
        page.RootElement.GetProperty("data")[2].GetProperty("status").GetString().ShouldBe("Closed");
        secondPage.RootElement.GetProperty("data").EnumerateArray().Select(c => c.GetProperty("id").GetGuid())
            .ShouldBe([first]);
    }

    [Fact]
    public async Task GetPatientConsultations_WithInvalidPageSize_ReturnsBadRequestProblem()
    {
        var (admin, _, _, patientId) = await LinkedDoctorAsync();

        var response = await GetPatientConsultationsAsync(admin, patientId, "?pageSize=0");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetPatientConsultations_WithUnknownPatient_ReturnsNotFoundProblem()
    {
        var admin = await AdminAsync();

        var response = await GetPatientConsultationsAsync(admin, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task ConsultationEndpoints_WithUnknownId_ReturnNotFoundProblem()
    {
        var (_, doctorClient, _, _) = await LinkedDoctorAsync();
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await GetConsultationAsync(doctorClient, id),
            await UpdateConsultationAsync(doctorClient, id, "Motivo", null, null),
            await CloseConsultationAsync(doctorClient, id, "Diagnóstico"),
        };

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, response.RequestMessage?.RequestUri?.ToString());
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    [Fact]
    public async Task ConsultationEndpoints_AsUnlinkedDoctor_ReturnForbiddenProblem()
    {
        var (_, doctorClient, _, patientId) = await LinkedDoctorAsync();
        var id = await StartConsultationAsync(doctorClient, patientId);
        var unlinked = await factory.AuthenticateAsAsync(Role.Doctor, TestContext.Current.CancellationToken);

        await AssertAllRoutesAsync(unlinked, id, patientId, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ConsultationEndpoints_AsReceptionist_ReturnForbiddenProblem()
    {
        var (_, doctorClient, _, patientId) = await LinkedDoctorAsync();
        var id = await StartConsultationAsync(doctorClient, patientId);
        var reception = await factory.AuthenticateAsAsync(Role.Receptionist, TestContext.Current.CancellationToken);

        await AssertAllRoutesAsync(reception, id, patientId, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WriteEndpoints_AsAdmin_ReturnForbiddenProblem()
    {
        var (admin, doctorClient, _, patientId) = await LinkedDoctorAsync();
        var id = await StartConsultationAsync(doctorClient, patientId);

        var responses = new[]
        {
            await PostConsultationAsync(admin, NewConsultationBody(patientId)),
            await UpdateConsultationAsync(admin, id, "Motivo", null, null),
            await CloseConsultationAsync(admin, id, "Diagnóstico"),
        };

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, response.RequestMessage?.RequestUri?.ToString());
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    [Fact]
    public async Task ConsultationEndpoints_WithoutToken_ReturnUnauthorizedProblem()
    {
        await AssertAllRoutesAsync(factory.CreateClient(), Guid.NewGuid(), Guid.NewGuid(), HttpStatusCode.Unauthorized);
    }

    // Las cinco rutas del módulo con el mismo código de estado esperado
    private static async Task AssertAllRoutesAsync(HttpClient client, Guid consultationId, Guid patientId, HttpStatusCode expected)
    {
        var responses = new[]
        {
            await PostConsultationAsync(client, NewConsultationBody(patientId)),
            await UpdateConsultationAsync(client, consultationId, "Motivo", null, null),
            await CloseConsultationAsync(client, consultationId, "Diagnóstico"),
            await GetConsultationAsync(client, consultationId),
            await GetPatientConsultationsAsync(client, patientId),
        };

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(expected, response.RequestMessage?.RequestUri?.ToString());
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    // Admin, un doctor nuevo con horario y su cliente vinculado, y un paciente nuevo
    private async Task<(HttpClient Admin, HttpClient DoctorClient, Guid DoctorId, Guid PatientId)> LinkedDoctorAsync()
    {
        var admin = await AdminAsync();
        var (patientId, doctorId) = await CreatePatientAndDoctorAsync(admin);
        var doctorClient = await AuthenticateAsLinkedDoctorAsync(factory, admin, doctorId);
        return (admin, doctorClient, doctorId, patientId);
    }

    private Task<HttpClient> AdminAsync() => factory.AuthenticateAsAsync(Role.Admin, TestContext.Current.CancellationToken);
}
