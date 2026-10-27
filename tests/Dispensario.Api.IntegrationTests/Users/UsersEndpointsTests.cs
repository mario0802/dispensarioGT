using System.Net;
using System.Net.Http.Json;
using Dispensario.Api.IntegrationTests.Common;
using Dispensario.Domain.Users;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Users;

public sealed class UsersEndpointsTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    private const string UsersRoute = "/api/users";

    [Theory]
    [InlineData("Admin")]
    [InlineData("Doctor")]
    [InlineData("Receptionist")]
    public async Task RegisterUser_AsAdmin_CreatesUserWithRequestedRole(string role)
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync(UsersRoute, NewUserBody(role), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var created = await ReadJsonAsync(response);
        var id = created.RootElement.GetProperty("id").GetGuid();
        response.Headers.Location?.ToString().ShouldBe($"/api/users/{id}");

        using var user = await ReadJsonAsync(await admin.GetAsync($"{UsersRoute}/{id}", TestContext.Current.CancellationToken));
        user.RootElement.GetProperty("role").GetString().ShouldBe(role);
        user.RootElement.GetProperty("doctorId").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        user.RootElement.GetProperty("createdAtUtc").GetDateTime().ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
    }

    [Fact]
    public async Task RegisterUser_WithoutRole_ReturnsValidationProblem()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync(UsersRoute, NewUserBody(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("ValidationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RegisterUser_WithDuplicateEmail_ReturnsConflictProblem()
    {
        var admin = await AdminAsync();
        var body = NewUserBody("Doctor");
        await CreateUserAsync(admin, "Doctor", body);
        var duplicate = NewUserBody("Doctor");
        duplicate["email"] = body["email"];

        var response = await admin.PostAsJsonAsync(UsersRoute, duplicate, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetUsers_WithRoleFilter_ListsOnlyThatRole()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateUserAsync(admin, "Doctor");

        var response = await admin.GetAsync($"{UsersRoute}?role=Doctor&pageSize=100", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("pageIndex").GetInt32().ShouldBe(0);
        json.RootElement.GetProperty("pageSize").GetInt32().ShouldBe(100);
        json.RootElement.GetProperty("count").GetInt64().ShouldBeGreaterThan(0);
        var data = json.RootElement.GetProperty("data").EnumerateArray().ToList();
        data.ShouldAllBe(u => u.GetProperty("role").GetString() == "Doctor");
        data.ShouldContain(u => u.GetProperty("id").GetGuid() == doctorId);
    }

    [Fact]
    public async Task GetUsers_WithSearchAndIsActive_FiltersUsers()
    {
        var admin = await AdminAsync();
        var body = NewUserBody("Receptionist");
        var id = await CreateUserAsync(admin, "Receptionist", body);
        (await admin.PostAsync($"{UsersRoute}/{id}/deactivate", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        var search = body["documentNumber"]!.ToString()!.ToUpperInvariant();

        using var inactive = await ReadJsonAsync(await admin.GetAsync(
            $"{UsersRoute}?search={search}&isActive=false", TestContext.Current.CancellationToken));
        using var active = await ReadJsonAsync(await admin.GetAsync(
            $"{UsersRoute}?search={search}&isActive=true", TestContext.Current.CancellationToken));

        inactive.RootElement.GetProperty("count").GetInt64().ShouldBe(1);
        active.RootElement.GetProperty("count").GetInt64().ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetUsers_WithPageSizeOutOfRange_ReturnsBadRequestProblem(int pageSize)
    {
        var admin = await AdminAsync();

        var response = await admin.GetAsync($"{UsersRoute}?pageSize={pageSize}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetUserById_WithUnknownId_ReturnsNotFoundProblem()
    {
        var admin = await AdminAsync();

        var response = await admin.GetAsync($"{UsersRoute}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task UpdateUserProfile_AsAdmin_UpdatesNameAndClearsPhone()
    {
        var admin = await AdminAsync();
        var id = await CreateUserAsync(admin, "Doctor");

        var response = await admin.PutAsJsonAsync(
            $"{UsersRoute}/{id}", new { firstName = "Lucía", lastName = "Vera", phone = (string?)null }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var user = await ReadJsonAsync(await admin.GetAsync($"{UsersRoute}/{id}", TestContext.Current.CancellationToken));
        user.RootElement.GetProperty("firstName").GetString().ShouldBe("Lucía");
        user.RootElement.GetProperty("lastName").GetString().ShouldBe("Vera");
        user.RootElement.GetProperty("phone").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task UpdateUserProfile_WithUnknownId_ReturnsNotFoundProblem()
    {
        var admin = await AdminAsync();

        var response = await admin.PutAsJsonAsync(
            $"{UsersRoute}/{Guid.NewGuid()}", new { firstName = "Lucía", lastName = "Vera" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResetUserPassword_AsAdmin_AllowsLoginWithNewPassword()
    {
        var admin = await AdminAsync();
        var body = NewUserBody("Receptionist");
        var id = await CreateUserAsync(admin, "Receptionist", body);

        var response = await admin.PutAsJsonAsync(
            $"{UsersRoute}/{id}/password", new { newPassword = "Reset-Passw0rd" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var client = factory.CreateClient();
        (await LoginAsync(client, body["email"]!, "Reset-Passw0rd")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoginAsync(client, body["email"]!, Password)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetUserPassword_WithWeakPassword_ReturnsValidationProblem()
    {
        var admin = await AdminAsync();
        var id = await CreateUserAsync(admin, "Receptionist");

        var response = await admin.PutAsJsonAsync(
            $"{UsersRoute}/{id}/password", new { newPassword = "weak" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetUserPassword_OnOwnUser_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();
        var ownId = await GetCurrentUserIdAsync(admin);

        var response = await admin.PutAsJsonAsync(
            $"{UsersRoute}/{ownId}/password", new { newPassword = "Reset-Passw0rd" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task DeactivateAndActivate_AsAdmin_BlockAndRestoreLogin()
    {
        var admin = await AdminAsync();
        var body = NewUserBody("Doctor");
        var id = await CreateUserAsync(admin, "Doctor", body);
        var client = factory.CreateClient();

        var deactivate = await admin.PostAsync($"{UsersRoute}/{id}/deactivate", null, TestContext.Current.CancellationToken);

        deactivate.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LoginAsync(client, body["email"]!, Password)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var activate = await admin.PostAsync($"{UsersRoute}/{id}/activate", null, TestContext.Current.CancellationToken);

        activate.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LoginAsync(client, body["email"]!, Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivate_InactiveUser_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();
        var id = await CreateUserAsync(admin, "Receptionist");
        (await admin.PostAsync($"{UsersRoute}/{id}/deactivate", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var response = await admin.PostAsync($"{UsersRoute}/{id}/deactivate", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Activate_ActiveUser_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();
        var id = await CreateUserAsync(admin, "Receptionist");

        var response = await admin.PostAsync($"{UsersRoute}/{id}/activate", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deactivate_OwnUser_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();
        var ownId = await GetCurrentUserIdAsync(admin);

        var response = await admin.PostAsync($"{UsersRoute}/{ownId}/deactivate", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Deactivate_WithUnknownId_ReturnsNotFoundProblem()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsync($"{UsersRoute}/{Guid.NewGuid()}/deactivate", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(Role.Receptionist)]
    [InlineData(Role.Doctor)]
    public async Task AdminOnlyEndpoints_AsNonAdmin_ReturnForbiddenProblem(Role role)
    {
        var client = await factory.AuthenticateAsAsync(role, TestContext.Current.CancellationToken);

        foreach (var request in AdminOnlyRequests())
        {
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{request.Method} {request.RequestUri}");
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    [Fact]
    public async Task UsersEndpoints_WithoutToken_ReturnUnauthorizedProblem()
    {
        var client = factory.CreateClient();
        var requests = AdminOnlyRequests()
            .Append(new HttpRequestMessage(HttpMethod.Put, $"{UsersRoute}/me") { Content = JsonContent.Create(new { firstName = "A", lastName = "B" }) })
            .Append(new HttpRequestMessage(HttpMethod.Put, $"{UsersRoute}/me/password")
            {
                Content = JsonContent.Create(new { currentPassword = "Passw0rd-Old", newPassword = "Passw0rd-New" }),
            });

        foreach (var request in requests)
        {
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{request.Method} {request.RequestUri}");
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }
    }

    private Task<HttpClient> AdminAsync() => factory.AuthenticateAsAsync(Role.Admin, TestContext.Current.CancellationToken);

    private static IEnumerable<HttpRequestMessage> AdminOnlyRequests()
    {
        var id = Guid.NewGuid();
        yield return new HttpRequestMessage(HttpMethod.Post, UsersRoute) { Content = JsonContent.Create(NewUserBody("Doctor")) };
        yield return new HttpRequestMessage(HttpMethod.Get, UsersRoute);
        yield return new HttpRequestMessage(HttpMethod.Get, $"{UsersRoute}/{id}");
        yield return new HttpRequestMessage(HttpMethod.Put, $"{UsersRoute}/{id}") { Content = JsonContent.Create(new { firstName = "A", lastName = "B" }) };
        yield return new HttpRequestMessage(HttpMethod.Put, $"{UsersRoute}/{id}/password") { Content = JsonContent.Create(new { newPassword = "Passw0rd-New" }) };
        yield return new HttpRequestMessage(HttpMethod.Post, $"{UsersRoute}/{id}/deactivate");
        yield return new HttpRequestMessage(HttpMethod.Post, $"{UsersRoute}/{id}/activate");
    }
}
