using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dispensario.Application.UnitTests.Persistence;

public sealed class UtcDateTimeConventionTests : IDisposable
{
    private readonly TestDb _db = new();

    [Fact]
    public async Task SaveChangesAsync_WithUtcDateTime_ReadsBackUtcKindAndSameValue()
    {
        var scheduledAtUtc = new DateTime(2026, 10, 26, 9, 30, 0, DateTimeKind.Utc);
        await SaveAsync(TestAggregate.Create(FullName.Create("Ana", "Pérez"), scheduledAtUtc));

        await using var context = _db.CreateContext();
        var loaded = await context.Aggregates.SingleAsync(TestContext.Current.CancellationToken);

        loaded.ScheduledAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.ScheduledAtUtc.ShouldBe(scheduledAtUtc);
    }

    [Fact]
    public async Task SaveChangesAsync_WithAuditDates_ReadsBackUtcKind()
    {
        // CreatedAtUtc lo fija el interceptor de auditoría con la hora del FakeTimeProvider
        var createdAtUtc = TestDb.StartTime.UtcDateTime;
        var lastModifiedAtUtc = new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc);
        var aggregate = TestAggregate.Create(FullName.Create("Ana", "Pérez"), createdAtUtc);
        aggregate.LastModifiedAtUtc = lastModifiedAtUtc;
        await SaveAsync(aggregate);

        await using var context = _db.CreateContext();
        var loaded = await context.Aggregates.SingleAsync(TestContext.Current.CancellationToken);

        loaded.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.CreatedAtUtc.ShouldBe(createdAtUtc);
        loaded.LastModifiedAtUtc.ShouldNotBeNull();
        loaded.LastModifiedAtUtc.Value.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.LastModifiedAtUtc.ShouldBe(lastModifiedAtUtc);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNullNullableDateTime_ReadsBackNull()
    {
        var scheduledAtUtc = new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);
        await SaveAsync(TestAggregate.Create(FullName.Create("Ana", "Pérez"), scheduledAtUtc));

        await using var context = _db.CreateContext();
        var loaded = await context.Aggregates.SingleAsync(TestContext.Current.CancellationToken);

        loaded.LastModifiedAtUtc.ShouldBeNull();
    }

    public void Dispose() => _db.Dispose();

    private async Task SaveAsync(TestAggregate aggregate)
    {
        await using var context = _db.CreateContext();
        context.Aggregates.Add(aggregate);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
