using BuildingBlocks.Exceptions;
using Dispensario.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Dispensario.Application.UnitTests.Common;

public sealed class ClinicTimeZoneTests
{
    [Fact]
    public void ToUtc_WithGuayaquilLocalTime_AddsFiveHours()
    {
        var timeZone = CreateTimeZone("America/Guayaquil");

        var utc = timeZone.ToUtc(new DateTime(2026, 11, 3, 9, 30, 0));

        utc.ShouldBe(new DateTime(2026, 11, 3, 14, 30, 0, DateTimeKind.Utc));
        utc.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void ToLocal_WithGuayaquilUtcTime_SubtractsFiveHours()
    {
        var timeZone = CreateTimeZone("America/Guayaquil");

        var local = timeZone.ToLocal(new DateTime(2026, 11, 3, 14, 30, 0, DateTimeKind.Utc));

        local.ShouldBe(new DateTime(2026, 11, 3, 9, 30, 0));
        local.Kind.ShouldBe(DateTimeKind.Unspecified);
    }

    [Fact]
    public void Today_NearUtcMidnight_ReturnsPreviousLocalDate()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 10, 28, 4, 0, 0, TimeSpan.Zero));
        var timeZone = CreateTimeZone("America/Guayaquil", timeProvider);

        timeZone.Today.ShouldBe(new DateOnly(2026, 10, 27));
    }

    [Fact]
    public void ToUtc_WithTimeSkippedByDaylightSaving_ThrowsBadRequestException()
    {
        var timeZone = CreateTimeZone("America/New_York");

        // El 8 de marzo de 2026 Nueva York pasa de 02:00 a 03:00: las 02:30 no existen
        Action act = () => timeZone.ToUtc(new DateTime(2026, 3, 8, 2, 30, 0));

        act.ShouldThrow<BadRequestException>().Message.ShouldBe("Start time does not exist in the clinic time zone.");
    }

    [Fact]
    public void AddApplication_WithUnknownTimeZone_FailsOptionsValidation()
    {
        using var provider = BuildServices("Mars/Olympus_Mons");

        var act = () => provider.GetRequiredService<IOptions<ClinicOptions>>().Value;

        act.ShouldThrow<OptionsValidationException>().Message.ShouldContain("Clinic:TimeZone is not a valid time zone.");
    }

    [Fact]
    public void AddApplication_WithValidTimeZone_BindsClinicOptions()
    {
        using var provider = BuildServices("America/Guayaquil");

        provider.GetRequiredService<IOptions<ClinicOptions>>().Value.TimeZone.ShouldBe("America/Guayaquil");
    }

    private static ClinicTimeZone CreateTimeZone(string timeZoneId, TimeProvider? timeProvider = null) =>
        new(Options.Create(new ClinicOptions { TimeZone = timeZoneId }), timeProvider ?? new FakeTimeProvider());

    private static ServiceProvider BuildServices(string timeZoneId)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Clinic:TimeZone"] = timeZoneId })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(configuration);
        return services.BuildServiceProvider();
    }
}
