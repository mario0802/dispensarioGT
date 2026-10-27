using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dispensario.Api.IntegrationTests.Common;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Auth;

public sealed class RegisterEndpointTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    private const string RegisterRoute = "/api/auth/register";

    [Fact]
    public async Task Register_WithoutToken_CreatesReceptionistThatCanLogIn()
    {
        var client = factory.CreateClient();
        var body = NewUserBody();

        var response = await client.PostAsJsonAsync(RegisterRoute, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var json = await ReadJsonAsync(response);
        var id = json.RootElement.GetProperty("id").GetGuid();
        response.Headers.Location?.ToString().ShouldBe($"/api/users/{id}");
        (await GetRoleAfterLoginAsync(body)).ShouldBe("Receptionist");
    }

    [Fact]
    public async Task Register_WithRoleInBody_IgnoresRole()
    {
        var client = factory.CreateClient();
        var body = NewUserBody(role: "Admin");

        var response = await client.PostAsJsonAsync(RegisterRoute, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await GetRoleAfterLoginAsync(body)).ShouldBe("Receptionist");
    }

    [Fact]
    public async Task Register_WithDuplicateEmailInOtherCase_ReturnsConflictProblem()
    {
        var client = factory.CreateClient();
        var body = NewUserBody();
        (await client.PostAsJsonAsync(RegisterRoute, body, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        var duplicate = NewUserBody();
        duplicate["email"] = body["email"]!.ToString()!.ToUpperInvariant();

        var response = await client.PostAsJsonAsync(RegisterRoute, duplicate, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Register_WithDuplicateIdentification_ReturnsConflictProblem()
    {
        var client = factory.CreateClient();
        var body = NewUserBody();
        (await client.PostAsJsonAsync(RegisterRoute, body, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        var duplicate = NewUserBody();
        duplicate["documentNumber"] = body["documentNumber"];

        var response = await client.PostAsJsonAsync(RegisterRoute, duplicate, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("1234567890")]
    public async Task Register_WithWeakPassword_ReturnsValidationProblem(string password)
    {
        var client = factory.CreateClient();
        var body = NewUserBody();
        body["password"] = password;

        var response = await client.PostAsJsonAsync(RegisterRoute, body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("ValidationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    private async Task<string> GetRoleAfterLoginAsync(Dictionary<string, object?> body)
    {
        var client = factory.CreateClient();
        var login = await LoginAsync(client, body["email"]!, Password);
        login.EnsureSuccessStatusCode();
        using var token = await ReadJsonAsync(login);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.RootElement.GetProperty("accessToken").GetString());

        using var me = await ReadJsonAsync(await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken));
        return me.RootElement.GetProperty("role").GetString()!;
    }
}
