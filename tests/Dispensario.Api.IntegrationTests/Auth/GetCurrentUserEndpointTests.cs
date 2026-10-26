using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dispensario.Api.IntegrationTests.Common;
using Dispensario.Domain.Users;
using Dispensario.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Dispensario.Api.IntegrationTests.Auth;

public sealed class GetCurrentUserEndpointTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    private const string MeRoute = "/api/auth/me";

    [Fact]
    public async Task GetMe_WithAdminToken_ReturnsUserWithEnumsAsText()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAdminAsync(client));

        var response = await client.GetAsync(MeRoute, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("email").GetString().ShouldBe(DispensarioApiFactory.AdminEmail);
        body.RootElement.GetProperty("role").GetString().ShouldBe("Admin");
        body.RootElement.GetProperty("identificationType").GetString().ShouldBe("NationalId");
        body.RootElement.GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Doctor)]
    [InlineData(Role.Receptionist)]
    public async Task GetMe_AuthenticatedAsRole_ReturnsThatRole(Role role)
    {
        var client = await factory.AuthenticateAsAsync(role, TestContext.Current.CancellationToken);

        var response = await client.GetAsync(MeRoute, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await ReadJsonAsync(response);
        body.RootElement.GetProperty("role").GetString().ShouldBe(role.ToString());
    }

    [Fact]
    public async Task GetMe_WithoutToken_ReturnsUnauthorizedProblem()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(MeRoute, TestContext.Current.CancellationToken);

        ShouldBeUnauthorizedProblem(response);
    }

    [Fact]
    public async Task GetMe_WithTamperedToken_ReturnsUnauthorizedProblem()
    {
        var client = factory.CreateClient();
        var token = await LoginAdminAsync(client);
        // Cambiar el último carácter rompe la firma
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync(MeRoute, TestContext.Current.CancellationToken);

        ShouldBeUnauthorizedProblem(response);
    }

    [Fact]
    public async Task GetMe_WithTokenSignedWithOtherKey_ReturnsUnauthorizedProblem()
    {
        var client = factory.CreateClient();
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = DispensarioApiFactory.JwtIssuer,
            Audience = DispensarioApiFactory.JwtAudience,
            Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = Guid.NewGuid().ToString(), ["role"] = "Admin" },
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-signing-key-with-at-least-32-chars")),
                SecurityAlgorithms.HmacSha256),
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(MeRoute, TestContext.Current.CancellationToken);

        ShouldBeUnauthorizedProblem(response);
    }

    [Fact]
    public async Task GetMe_AfterUserDeactivated_ReturnsUnauthorizedProblem()
    {
        var client = await factory.AuthenticateAsAsync(Role.Receptionist, TestContext.Current.CancellationToken);
        var userId = await GetCurrentUserIdAsync(client);
        await UpdateUserAsync(userId, (dbContext, user) => user.Deactivate());

        var response = await client.GetAsync(MeRoute, TestContext.Current.CancellationToken);

        ShouldBeUnauthorizedProblem(response);
    }

    [Fact]
    public async Task GetMe_AfterUserDeleted_ReturnsNotFoundProblem()
    {
        var client = await factory.AuthenticateAsAsync(Role.Receptionist, TestContext.Current.CancellationToken);
        var userId = await GetCurrentUserIdAsync(client);
        await UpdateUserAsync(userId, (dbContext, user) => dbContext.Users.Remove(user));

        var response = await client.GetAsync(MeRoute, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    private static async Task<string> LoginAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = DispensarioApiFactory.AdminEmail, password = DispensarioApiFactory.AdminPassword },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = await ReadJsonAsync(response);
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<UserId> GetCurrentUserIdAsync(HttpClient client)
    {
        var response = await client.GetAsync(MeRoute, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = await ReadJsonAsync(response);
        return new UserId(body.RootElement.GetProperty("id").GetGuid());
    }

    private async Task UpdateUserAsync(UserId userId, Action<DispensarioDbContext, User> change)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DispensarioDbContext>();
        var user = await dbContext.Users.SingleAsync(u => u.Id == userId, TestContext.Current.CancellationToken);
        change(dbContext, user);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static void ShouldBeUnauthorizedProblem(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
