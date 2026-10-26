using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dispensario.Api.IntegrationTests.Common;

namespace Dispensario.Api.IntegrationTests.Auth;

public sealed class LoginEndpointTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsBearerToken()
    {
        var client = factory.CreateClient();

        var response = await LoginAsync(client, DispensarioApiFactory.AdminEmail, DispensarioApiFactory.AdminPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("accessToken").GetString().ShouldNotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("tokenType").GetString().ShouldBe("Bearer");
        body.RootElement.GetProperty("expiresAtUtc").GetDateTime().ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Theory]
    [InlineData(DispensarioApiFactory.AdminEmail, "wrong-password")]
    [InlineData("nobody@dispensario.local", DispensarioApiFactory.AdminPassword)]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorizedProblem(string email, string password)
    {
        var client = factory.CreateClient();

        var response = await LoginAsync(client, email, password);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("detail").GetString().ShouldBe("Invalid credentials.");
    }

    [Fact]
    public async Task Login_WithEmptyEmail_ReturnsValidationProblem()
    {
        var client = factory.CreateClient();

        var response = await LoginAsync(client, "", DispensarioApiFactory.AdminPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("ValidationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password }, TestContext.Current.CancellationToken);

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
