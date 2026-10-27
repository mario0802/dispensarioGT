using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dispensario.Api.IntegrationTests.Common;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Users;

public sealed class CurrentUserEndpointsTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    [Fact]
    public async Task UpdateMe_WithValidData_IsShownByAuthMe()
    {
        var (client, _) = await RegisterAndLoginAsync();

        var response = await client.PutAsJsonAsync(
            "/api/users/me", new { firstName = "Marta", lastName = "Ríos", phone = (string?)null }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var me = await ReadJsonAsync(await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken));
        me.RootElement.GetProperty("firstName").GetString().ShouldBe("Marta");
        me.RootElement.GetProperty("lastName").GetString().ShouldBe("Ríos");
        me.RootElement.GetProperty("phone").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task UpdateMe_WithEmptyFirstName_ReturnsValidationProblem()
    {
        var (client, _) = await RegisterAndLoginAsync();

        var response = await client.PutAsJsonAsync(
            "/api/users/me", new { firstName = "", lastName = "Ríos" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_AllowsLoginWithNewPassword()
    {
        var (client, email) = await RegisterAndLoginAsync();

        var response = await client.PutAsJsonAsync(
            "/api/users/me/password", new { currentPassword = Password, newPassword = "Changed-Passw0rd" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var anonymous = factory.CreateClient();
        (await LoginAsync(anonymous, email, "Changed-Passw0rd")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoginAsync(anonymous, email, Password)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsBadRequestProblem()
    {
        var (client, _) = await RegisterAndLoginAsync();

        var response = await client.PutAsJsonAsync(
            "/api/users/me/password", new { currentPassword = "Wrong-Passw0rd", newPassword = "Changed-Passw0rd" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("detail").GetString().ShouldBe("Current password is incorrect.");
    }

    [Fact]
    public async Task ChangePassword_WithWeakNewPassword_ReturnsValidationProblem()
    {
        var (client, _) = await RegisterAndLoginAsync();

        var response = await client.PutAsJsonAsync(
            "/api/users/me/password", new { currentPassword = Password, newPassword = "weakpass" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("ValidationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task MeEndpoints_WithoutToken_ReturnUnauthorizedProblem()
    {
        var client = factory.CreateClient();

        var profile = await client.PutAsJsonAsync("/api/users/me", new { firstName = "A", lastName = "B" }, TestContext.Current.CancellationToken);
        var password = await client.PutAsJsonAsync(
            "/api/users/me/password", new { currentPassword = Password, newPassword = "Changed-Passw0rd" }, TestContext.Current.CancellationToken);

        profile.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        password.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // Autoregistro + login: da un recepcionista con contraseña conocida
    private async Task<(HttpClient Client, string Email)> RegisterAndLoginAsync()
    {
        var client = factory.CreateClient();
        var body = NewUserBody();
        (await client.PostAsJsonAsync("/api/auth/register", body, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var email = body["email"]!.ToString()!;
        var login = await LoginAsync(client, email, Password);
        login.EnsureSuccessStatusCode();
        using var token = await ReadJsonAsync(login);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.RootElement.GetProperty("accessToken").GetString());
        return (client, email);
    }
}
