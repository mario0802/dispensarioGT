using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dispensario.Api.IntegrationTests.Common;
using Dispensario.Domain.Users;
using static Dispensario.Api.IntegrationTests.Common.DoctorRequests;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Doctors;

public sealed class DoctorsEndpointsTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    [Fact]
    public async Task CreateDoctor_AsAdmin_CreatesDoctorWithDefaultDuration()
    {
        var admin = await AdminAsync();
        var body = NewDoctorBody();

        var response = await admin.PostAsJsonAsync(DoctorsRoute, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var created = await ReadJsonAsync(response);
        var id = created.RootElement.GetProperty("id").GetGuid();
        response.Headers.Location?.ToString().ShouldBe($"{DoctorsRoute}/{id}");

        using var doctor = await ReadJsonAsync(await admin.GetAsync($"{DoctorsRoute}/{id}", TestContext.Current.CancellationToken));
        doctor.RootElement.GetProperty("licenseNumber").GetString().ShouldBe(body["licenseNumber"]!.ToString()!.ToUpperInvariant());
        doctor.RootElement.GetProperty("appointmentDurationMinutes").GetInt32().ShouldBe(30);
        doctor.RootElement.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        doctor.RootElement.GetProperty("userId").ValueKind.ShouldBe(JsonValueKind.Null);
        doctor.RootElement.GetProperty("createdAtUtc").GetDateTime().ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateDoctor_WithDuplicateLicenseInOtherCase_ReturnsConflictProblem()
    {
        var admin = await AdminAsync();
        var body = NewDoctorBody();
        await CreateDoctorAsync(admin, body);
        var duplicate = NewDoctorBody();
        duplicate["licenseNumber"] = body["licenseNumber"]!.ToString()!.ToUpperInvariant();

        var response = await admin.PostAsJsonAsync(DoctorsRoute, duplicate, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData(32)]
    [InlineData(5)]
    [InlineData(125)]
    public async Task CreateDoctor_WithInvalidDuration_ReturnsValidationProblem(int minutes)
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync(DoctorsRoute, NewDoctorBody(minutes), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("ValidationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("email", null)]
    [InlineData("email", "not-an-email")]
    [InlineData("licenseNumber", "")]
    [InlineData("specialty", "")]
    public async Task CreateDoctor_WithMissingOrInvalidField_ReturnsBadRequest(string field, string? value)
    {
        var admin = await AdminAsync();
        var body = NewDoctorBody();
        body[field] = value;

        var response = await admin.PostAsJsonAsync(DoctorsRoute, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetDoctors_WithSpecialtyAndSearch_ListsDoctor()
    {
        var admin = await AdminAsync();
        var body = NewDoctorBody();
        var specialty = $"Spec-{Guid.NewGuid():N}"[..20];
        body["specialty"] = specialty;
        var id = await CreateDoctorAsync(admin, body);

        var response = await admin.GetAsync(
            $"{DoctorsRoute}?specialty={specialty.ToUpperInvariant()}&search={body["licenseNumber"]}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("pageIndex").GetInt32().ShouldBe(0);
        json.RootElement.GetProperty("pageSize").GetInt32().ShouldBe(10);
        json.RootElement.GetProperty("count").GetInt64().ShouldBe(1);
        json.RootElement.GetProperty("data")[0].GetProperty("id").GetGuid().ShouldBe(id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetDoctors_WithPageSizeOutOfRange_ReturnsBadRequestProblem(int pageSize)
    {
        var admin = await AdminAsync();

        var response = await admin.GetAsync($"{DoctorsRoute}?pageSize={pageSize}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetDoctorById_WithUnknownId_ReturnsNotFoundProblem()
    {
        var admin = await AdminAsync();

        var response = await admin.GetAsync($"{DoctorsRoute}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task UpdateDoctor_WithoutDuration_UpdatesFieldsAndKeepsDuration()
    {
        var admin = await AdminAsync();
        var id = await CreateDoctorAsync(admin, NewDoctorBody(45));
        var update = NewDoctorBody();
        update["specialty"] = "Cardiologia";
        update["phone"] = null;

        var response = await admin.PutAsJsonAsync($"{DoctorsRoute}/{id}", update, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var doctor = await ReadJsonAsync(await admin.GetAsync($"{DoctorsRoute}/{id}", TestContext.Current.CancellationToken));
        doctor.RootElement.GetProperty("specialty").GetString().ShouldBe("Cardiologia");
        doctor.RootElement.GetProperty("phone").ValueKind.ShouldBe(JsonValueKind.Null);
        doctor.RootElement.GetProperty("appointmentDurationMinutes").GetInt32().ShouldBe(45);
    }

    [Fact]
    public async Task UpdateDoctor_WithLicenseOfOtherDoctor_ReturnsConflictProblem()
    {
        var admin = await AdminAsync();
        var other = NewDoctorBody();
        await CreateDoctorAsync(admin, other);
        var id = await CreateDoctorAsync(admin);
        var update = NewDoctorBody();
        update["licenseNumber"] = other["licenseNumber"];

        var response = await admin.PutAsJsonAsync($"{DoctorsRoute}/{id}", update, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateDoctor_WithInvalidDuration_ReturnsValidationProblem()
    {
        var admin = await AdminAsync();
        var id = await CreateDoctorAsync(admin);

        var response = await admin.PutAsJsonAsync($"{DoctorsRoute}/{id}", NewDoctorBody(32), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateDoctor_WithUnknownId_ReturnsNotFoundProblem()
    {
        var admin = await AdminAsync();

        var response = await admin.PutAsJsonAsync($"{DoctorsRoute}/{Guid.NewGuid()}", NewDoctorBody(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateAndActivate_AsAdmin_ChangeStateAndRejectRepeats()
    {
        var admin = await AdminAsync();
        var body = NewDoctorBody();
        var id = await CreateDoctorAsync(admin, body);

        (await admin.PostAsync($"{DoctorsRoute}/{id}/deactivate", null, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using (var inactive = await ReadJsonAsync(await admin.GetAsync(
                   $"{DoctorsRoute}?isActive=false&search={body["licenseNumber"]}", TestContext.Current.CancellationToken)))
            inactive.RootElement.GetProperty("count").GetInt64().ShouldBe(1);
        (await admin.PostAsync($"{DoctorsRoute}/{id}/deactivate", null, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await admin.PostAsync($"{DoctorsRoute}/{id}/activate", null, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.PostAsync($"{DoctorsRoute}/{id}/activate", null, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deactivate_WithUnknownId_ReturnsNotFoundProblem()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsync($"{DoctorsRoute}/{Guid.NewGuid()}/deactivate", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(Role.Receptionist)]
    [InlineData(Role.Doctor)]
    public async Task ReadEndpoints_AsAnyRole_ReturnOk(Role role)
    {
        var id = await CreateDoctorAsync(await AdminAsync());
        var client = await factory.AuthenticateAsAsync(role, TestContext.Current.CancellationToken);

        (await client.GetAsync(DoctorsRoute, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync($"{DoctorsRoute}/{id}", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(Role.Receptionist)]
    [InlineData(Role.Doctor)]
    public async Task WriteEndpoints_AsNonAdmin_ReturnForbiddenProblem(Role role)
    {
        var client = await factory.AuthenticateAsAsync(role, TestContext.Current.CancellationToken);

        foreach (var request in WriteRequests())
        {
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{request.Method} {request.RequestUri}");
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    [Fact]
    public async Task DoctorsEndpoints_WithoutToken_ReturnUnauthorizedProblem()
    {
        var client = factory.CreateClient();
        var requests = WriteRequests()
            .Append(new HttpRequestMessage(HttpMethod.Get, DoctorsRoute))
            .Append(new HttpRequestMessage(HttpMethod.Get, $"{DoctorsRoute}/{Guid.NewGuid()}"));

        foreach (var request in requests)
        {
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{request.Method} {request.RequestUri}");
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    private Task<HttpClient> AdminAsync() => factory.AuthenticateAsAsync(Role.Admin, TestContext.Current.CancellationToken);

    private static IEnumerable<HttpRequestMessage> WriteRequests()
    {
        var id = Guid.NewGuid();
        yield return new HttpRequestMessage(HttpMethod.Post, DoctorsRoute) { Content = JsonContent.Create(NewDoctorBody()) };
        yield return new HttpRequestMessage(HttpMethod.Put, $"{DoctorsRoute}/{id}") { Content = JsonContent.Create(NewDoctorBody()) };
        yield return new HttpRequestMessage(HttpMethod.Post, $"{DoctorsRoute}/{id}/deactivate");
        yield return new HttpRequestMessage(HttpMethod.Post, $"{DoctorsRoute}/{id}/activate");
    }
}
