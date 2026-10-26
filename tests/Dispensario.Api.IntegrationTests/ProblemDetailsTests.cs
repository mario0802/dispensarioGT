using System.Net;
using System.Text.Json;
using Dispensario.Api.IntegrationTests.Common;

namespace Dispensario.Api.IntegrationTests;

public sealed class ProblemDetailsTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    [Fact]
    public async Task GetUnknownRoute_Always_ReturnsNotFoundProblemDetails()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
    }
}
