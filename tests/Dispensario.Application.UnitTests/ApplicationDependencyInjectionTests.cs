using BuildingBlocks.Behaviors;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dispensario.Application.UnitTests;

public sealed class ApplicationDependencyInjectionTests
{
    [Fact]
    public void AddApplication_WithEmptyLicenseKey_ResolvesSenderAndPublisher()
    {
        using var provider = BuildServices().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetService<ISender>();
        var publisher = scope.ServiceProvider.GetService<IPublisher>();

        sender.ShouldNotBeNull();
        publisher.ShouldNotBeNull();
    }

    [Fact]
    public void AddApplication_Always_RegistersLoggingBeforeValidationBehavior()
    {
        var services = BuildServices();

        var behaviors = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType)
            .ToList();

        behaviors.ShouldBe([typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)]);
    }

    private static ServiceCollection BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MediatR:LicenseKey"] = "" })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(configuration);
        return services;
    }
}
