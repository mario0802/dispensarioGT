using Dispensario.Application.Abstractions;
using Dispensario.Infrastructure;
using Dispensario.Infrastructure.Authentication;
using Dispensario.Infrastructure.Data;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Persistence;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_WithConnectionString_ResolvesSameDbContextInstance()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Data Source=:memory:",
        });
        using var scope = provider.CreateScope();

        var applicationDbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DispensarioDbContext>();

        applicationDbContext.ShouldBeSameAs(dbContext);
        dbContext.Model.ShouldNotBeNull();
    }

    [Fact]
    public void AddInfrastructure_WithoutHttpRequest_ResolvesCurrentUserAsAnonymous()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Data Source=:memory:",
        });
        using var scope = provider.CreateScope();

        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        currentUser.ShouldBeOfType<CurrentUser>();
        currentUser.UserId.ShouldBeNull();
        currentUser.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public void AddInfrastructure_WithoutConnectionString_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Should.Throw<InvalidOperationException>(() => services.AddInfrastructure(configuration));

        exception.Message.ShouldBe("Connection string 'Database' not found.");
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();

        // MediatR registra IPublisher en AddApplication (fila 05); aquí se sustituye
        services.AddSingleton(Substitute.For<IPublisher>());
        // El logging lo registra el host; DatabaseSeeder lo necesita
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
