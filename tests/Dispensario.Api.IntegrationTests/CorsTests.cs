using System.Net;
using Dispensario.Api.IntegrationTests.Common;

namespace Dispensario.Api.IntegrationTests;

public sealed class CorsTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";

    [Fact]
    public async Task Preflight_FromAllowedOrigin_AllowsTheRequest()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", DispensarioApiFactory.AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues(AllowOriginHeader).ShouldBe([DispensarioApiFactory.AllowedOrigin]);
    }

    [Fact]
    public async Task ErrorResponse_ForAllowedOrigin_IncludesAllowOriginHeader()
    {
        // El frontend necesita leer los ProblemDetails de los errores (aquí, un 401 sin token)
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Origin", DispensarioApiFactory.AllowedOrigin);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.GetValues(AllowOriginHeader).ShouldBe([DispensarioApiFactory.AllowedOrigin]);
    }

    [Fact]
    public async Task Request_FromUnknownOrigin_HasNoAllowOriginHeader()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://unknown.tests");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Contains(AllowOriginHeader).ShouldBeFalse();
    }
}
