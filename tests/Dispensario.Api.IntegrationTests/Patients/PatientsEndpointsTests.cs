using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dispensario.Api.IntegrationTests.Common;
using Dispensario.Domain.Users;
using static Dispensario.Api.IntegrationTests.Common.PatientRequests;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Patients;

public sealed class PatientsEndpointsTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    [Fact]
    public async Task CreatePatient_AsReceptionist_CreatesPatient()
    {
        var reception = await ReceptionAsync();
        var body = NewPatientBody();

        var response = await reception.PostAsJsonAsync(PatientsRoute, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var created = await ReadJsonAsync(response);
        var id = created.RootElement.GetProperty("id").GetGuid();
        response.Headers.Location?.ToString().ShouldBe($"{PatientsRoute}/{id}");

        using var patient = await ReadJsonAsync(await reception.GetAsync($"{PatientsRoute}/{id}", TestContext.Current.CancellationToken));
        var root = patient.RootElement;
        root.GetProperty("documentType").GetString().ShouldBe("NationalId");
        root.GetProperty("documentNumber").GetString().ShouldBe(body["documentNumber"]!.ToString());
        root.GetProperty("birthDate").GetString().ShouldBe("1990-05-14");
        root.GetProperty("gender").GetString().ShouldBe("Female");
        root.GetProperty("bloodType").GetString().ShouldBe("O+");
        root.GetProperty("address").GetProperty("street").GetString().ShouldBe("Av. Amazonas 123");
        root.GetProperty("address").GetProperty("state").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        root.GetProperty("createdAtUtc").GetDateTime().ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
    }

    [Fact]
    public async Task CreatePatient_AsAdminWithOnlyRequiredFields_CreatesPatientWithNullOptionals()
    {
        var admin = await AdminAsync();
        var body = NewPatientBody();
        foreach (var optional in new[] { "email", "phone", "address", "bloodType", "allergies" })
            body.Remove(optional);

        var response = await admin.PostAsJsonAsync(PatientsRoute, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var created = await ReadJsonAsync(response);
        using var patient = await ReadJsonAsync(await admin.GetAsync(
            $"{PatientsRoute}/{created.RootElement.GetProperty("id").GetGuid()}", TestContext.Current.CancellationToken));
        patient.RootElement.GetProperty("address").ValueKind.ShouldBe(JsonValueKind.Null);
        patient.RootElement.GetProperty("bloodType").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task CreatePatient_WithDuplicateDocument_ReturnsConflictProblem()
    {
        var reception = await ReceptionAsync();
        var body = NewPatientBody();
        await CreatePatientAsync(reception, body);
        var duplicate = NewPatientBody();
        duplicate["documentNumber"] = body["documentNumber"];

        var response = await reception.PostAsJsonAsync(PatientsRoute, duplicate, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task CreatePatient_WithSameNumberAndOtherType_CreatesPatient()
    {
        var reception = await ReceptionAsync();
        var body = NewPatientBody();
        await CreatePatientAsync(reception, body);
        var other = NewPatientBody();
        other["documentNumber"] = body["documentNumber"];
        other["documentType"] = "Passport";

        var response = await reception.PostAsJsonAsync(PatientsRoute, other, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePatient_WithUnknownBloodType_ReturnsValidationProblem()
    {
        var reception = await ReceptionAsync();
        var body = NewPatientBody();
        body["bloodType"] = "X+";

        var response = await reception.PostAsJsonAsync(PatientsRoute, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("ValidationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("gender", "Unknown")]
    [InlineData("documentType", "DriverLicense")]
    [InlineData("birthDate", "14/05/1990")]
    public async Task CreatePatient_WithUnreadableField_ReturnsBadRequestProblem(string field, string value)
    {
        var reception = await ReceptionAsync();
        var body = NewPatientBody();
        body[field] = value;

        var response = await reception.PostAsJsonAsync(PatientsRoute, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task CreatePatient_WithFutureBirthDate_ReturnsBadRequestProblem()
    {
        var reception = await ReceptionAsync();
        var body = NewPatientBody();
        body["birthDate"] = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2).ToString("yyyy-MM-dd");

        var response = await reception.PostAsJsonAsync(PatientsRoute, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetPatients_WithSearch_ListsPatient()
    {
        var reception = await ReceptionAsync();
        var body = NewPatientBody();
        var id = await CreatePatientAsync(reception, body);

        var response = await reception.GetAsync(
            $"{PatientsRoute}?search={body["lastName"]!.ToString()!.ToUpperInvariant()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("pageIndex").GetInt32().ShouldBe(0);
        json.RootElement.GetProperty("pageSize").GetInt32().ShouldBe(10);
        json.RootElement.GetProperty("count").GetInt64().ShouldBe(1);
        json.RootElement.GetProperty("data")[0].GetProperty("id").GetGuid().ShouldBe(id);
        json.RootElement.GetProperty("data")[0].GetProperty("bloodType").GetString().ShouldBe("O+");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetPatients_WithPageSizeOutOfRange_ReturnsBadRequestProblem(int pageSize)
    {
        var reception = await ReceptionAsync();

        var response = await reception.GetAsync($"{PatientsRoute}?pageSize={pageSize}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetPatientByDocument_WithExistingDocument_ReturnsPatient()
    {
        var reception = await ReceptionAsync();
        var body = NewPatientBody();
        var id = await CreatePatientAsync(reception, body);

        var response = await reception.GetAsync(
            $"{PatientsRoute}/by-document?type=NationalId&number={body["documentNumber"]}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("id").GetGuid().ShouldBe(id);
    }

    [Fact]
    public async Task GetPatientByDocument_WithUnknownNumber_ReturnsNotFoundProblem()
    {
        var reception = await ReceptionAsync();

        var response = await reception.GetAsync(
            $"{PatientsRoute}/by-document?type=NationalId&number=missing-{Guid.NewGuid().ToString("N")[..8]}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetPatientByDocument_WithBlankNumber_ReturnsBadRequestProblem()
    {
        var reception = await ReceptionAsync();

        var response = await reception.GetAsync($"{PatientsRoute}/by-document?type=NationalId&number=%20", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetPatientById_WithUnknownId_ReturnsNotFoundProblem()
    {
        var reception = await ReceptionAsync();

        var response = await reception.GetAsync($"{PatientsRoute}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task UpdatePatient_WithNewDocumentAndNullOptionals_UpdatesPatient()
    {
        var reception = await ReceptionAsync();
        var id = await CreatePatientAsync(reception);
        var update = NewPatientBody();
        update["documentType"] = "Passport";
        update["email"] = null;
        update["phone"] = null;
        update["address"] = null;
        update["bloodType"] = null;
        update["allergies"] = null;

        var response = await reception.PutAsJsonAsync($"{PatientsRoute}/{id}", update, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var patient = await ReadJsonAsync(await reception.GetAsync($"{PatientsRoute}/{id}", TestContext.Current.CancellationToken));
        var root = patient.RootElement;
        root.GetProperty("documentType").GetString().ShouldBe("Passport");
        root.GetProperty("documentNumber").GetString().ShouldBe(update["documentNumber"]!.ToString());
        foreach (var optional in new[] { "email", "phone", "address", "bloodType", "allergies" })
            root.GetProperty(optional).ValueKind.ShouldBe(JsonValueKind.Null, optional);
    }

    [Fact]
    public async Task UpdatePatient_WithDocumentOfOtherPatient_ReturnsConflictProblem()
    {
        var reception = await ReceptionAsync();
        var other = NewPatientBody();
        await CreatePatientAsync(reception, other);
        var id = await CreatePatientAsync(reception);
        var update = NewPatientBody();
        update["documentNumber"] = other["documentNumber"];

        var response = await reception.PutAsJsonAsync($"{PatientsRoute}/{id}", update, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdatePatient_WithUnknownId_ReturnsNotFoundProblem()
    {
        var reception = await ReceptionAsync();

        var response = await reception.PutAsJsonAsync($"{PatientsRoute}/{Guid.NewGuid()}", NewPatientBody(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateAndActivate_AsReceptionist_ChangeStateAndRejectRepeats()
    {
        var reception = await ReceptionAsync();
        var body = NewPatientBody();
        var id = await CreatePatientAsync(reception, body);

        (await reception.PostAsync($"{PatientsRoute}/{id}/deactivate", null, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using (var inactive = await ReadJsonAsync(await reception.GetAsync(
                   $"{PatientsRoute}?isActive=false&search={body["documentNumber"]}", TestContext.Current.CancellationToken)))
            inactive.RootElement.GetProperty("count").GetInt64().ShouldBe(1);
        using (var byDocument = await ReadJsonAsync(await reception.GetAsync(
                   $"{PatientsRoute}/by-document?type=NationalId&number={body["documentNumber"]}", TestContext.Current.CancellationToken)))
            byDocument.RootElement.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        (await reception.PostAsync($"{PatientsRoute}/{id}/deactivate", null, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await reception.PostAsync($"{PatientsRoute}/{id}/activate", null, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await reception.PostAsync($"{PatientsRoute}/{id}/activate", null, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deactivate_WithUnknownId_ReturnsNotFoundProblem()
    {
        var reception = await ReceptionAsync();

        var response = await reception.PostAsync($"{PatientsRoute}/{Guid.NewGuid()}/deactivate", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReadEndpoints_AsDoctor_ReturnOk()
    {
        var body = NewPatientBody();
        var id = await CreatePatientAsync(await ReceptionAsync(), body);
        var doctor = await factory.AuthenticateAsAsync(Role.Doctor, TestContext.Current.CancellationToken);

        (await doctor.GetAsync(PatientsRoute, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await doctor.GetAsync($"{PatientsRoute}/{id}", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await doctor.GetAsync($"{PatientsRoute}/by-document?type=NationalId&number={body["documentNumber"]}", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WriteEndpoints_AsDoctor_ReturnForbiddenProblem()
    {
        var doctor = await factory.AuthenticateAsAsync(Role.Doctor, TestContext.Current.CancellationToken);

        foreach (var request in WriteRequests())
        {
            var response = await doctor.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{request.Method} {request.RequestUri}");
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    [Fact]
    public async Task PatientsEndpoints_WithoutToken_ReturnUnauthorizedProblem()
    {
        var client = factory.CreateClient();
        var requests = WriteRequests()
            .Append(new HttpRequestMessage(HttpMethod.Get, PatientsRoute))
            .Append(new HttpRequestMessage(HttpMethod.Get, $"{PatientsRoute}/{Guid.NewGuid()}"))
            .Append(new HttpRequestMessage(HttpMethod.Get, $"{PatientsRoute}/by-document?type=NationalId&number=1"));

        foreach (var request in requests)
        {
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{request.Method} {request.RequestUri}");
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    private Task<HttpClient> AdminAsync() => factory.AuthenticateAsAsync(Role.Admin, TestContext.Current.CancellationToken);

    private Task<HttpClient> ReceptionAsync() => factory.AuthenticateAsAsync(Role.Receptionist, TestContext.Current.CancellationToken);

    private static IEnumerable<HttpRequestMessage> WriteRequests()
    {
        var id = Guid.NewGuid();
        yield return new HttpRequestMessage(HttpMethod.Post, PatientsRoute) { Content = JsonContent.Create(NewPatientBody()) };
        yield return new HttpRequestMessage(HttpMethod.Put, $"{PatientsRoute}/{id}") { Content = JsonContent.Create(NewPatientBody()) };
        yield return new HttpRequestMessage(HttpMethod.Post, $"{PatientsRoute}/{id}/deactivate");
        yield return new HttpRequestMessage(HttpMethod.Post, $"{PatientsRoute}/{id}/activate");
    }
}
