using System.Net;
using Dispensario.Api.IntegrationTests.Common;
using Dispensario.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dispensario.Api.IntegrationTests;

public sealed class HealthEndpointTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    [Fact]
    public async Task GetHealth_WithReachableDatabase_ReturnsHealthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public void DbContext_InTests_UsesInMemorySqlite()
    {
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<DispensarioDbContext>();

        dbContext.Database.GetConnectionString().ShouldBe(DispensarioApiFactory.InMemoryConnectionString);
    }
}
