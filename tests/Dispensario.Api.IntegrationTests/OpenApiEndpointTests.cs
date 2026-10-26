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

    [Fact]
    public async Task GetScalar_InDevelopment_ReturnsHtml()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }
}
