using System.Net;
using System.Text.Json;
using Dispensario.Api.IntegrationTests.Common;

namespace Dispensario.Api.IntegrationTests;

public sealed class OpenApiEndpointTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    [Fact]
    public async Task GetOpenApiDocument_InDevelopment_DeclaresBearerScheme()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var bearer = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        bearer.GetProperty("type").GetString().ShouldBe("http");
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");
        bearer.GetProperty("bearerFormat").GetString().ShouldBe("JWT");
        document.RootElement.GetProperty("security")[0].TryGetProperty("Bearer", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("/api/auth/register", "post", "Register", "Auth")]
    [InlineData("/api/users", "post", "RegisterUser", "Users")]
    [InlineData("/api/users", "get", "GetUsers", "Users")]
    [InlineData("/api/users/{id}", "get", "GetUserById", "Users")]
    [InlineData("/api/users/{id}", "put", "UpdateUserProfile", "Users")]
    [InlineData("/api/users/{id}/password", "put", "ResetUserPassword", "Users")]
    [InlineData("/api/users/{id}/deactivate", "post", "DeactivateUser", "Users")]
    [InlineData("/api/users/{id}/activate", "post", "ActivateUser", "Users")]
    [InlineData("/api/users/me", "put", "UpdateCurrentUserProfile", "Users")]
    [InlineData("/api/users/me/password", "put", "ChangePassword", "Users")]
    public async Task GetOpenApiDocument_InDevelopment_DescribesUserRoutes(string path, string method, string operationId, string tag)
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);
        operation.GetProperty("operationId").GetString().ShouldBe(operationId);
        operation.GetProperty("tags")[0].GetString().ShouldBe(tag);
        operation.GetProperty("summary").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetScalar_InDevelopment_ReturnsHtml()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }
}
