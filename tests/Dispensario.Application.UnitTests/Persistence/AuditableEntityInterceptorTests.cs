using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Persistence;

public sealed class AuditableEntityInterceptorTests : IDisposable
{
    private static readonly DateTime ScheduledAtUtc = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);

    private readonly TestDb _db = new();

    [Fact]
    public async Task SaveChangesAsync_WhenAdded_SetsCreatedFields()
    {
        var userId = Guid.CreateVersion7();
        _db.CurrentUser.UserId.Returns(userId);
        var aggregate = TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc);

        await AddAsync(aggregate);

        var loaded = await LoadAsync(aggregate.Id);
        loaded.CreatedAtUtc.ShouldBe(TestDb.StartTime.UtcDateTime);
        loaded.CreatedBy.ShouldBe(userId.ToString());
        loaded.LastModifiedAtUtc.ShouldBeNull();
        loaded.LastModifiedBy.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenModified_SetsLastModifiedFields()
    {
        var aggregate = TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc);
        await AddAsync(aggregate);
        var userId = Guid.CreateVersion7();
        _db.CurrentUser.UserId.Returns(userId);
        _db.TimeProvider.Advance(TimeSpan.FromHours(1));

        await using (var context = _db.CreateContext())
        {
            var tracked = await context.Aggregates.SingleAsync(x => x.Id == aggregate.Id, TestContext.Current.CancellationToken);
            tracked.Rename(FullName.Create("Ana María", "Pérez"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var loaded = await LoadAsync(aggregate.Id);
        loaded.CreatedAtUtc.ShouldBe(TestDb.StartTime.UtcDateTime);
        loaded.LastModifiedAtUtc.ShouldBe(TestDb.StartTime.AddHours(1).UtcDateTime);
        loaded.LastModifiedBy.ShouldBe(userId.ToString());
    }

    [Fact]
    public async Task SaveChangesAsync_WhenOnlyOwnedEntityAdded_SetsLastModifiedAtUtc()
    {
        var aggregate = TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc);
        await AddAsync(aggregate);
        _db.TimeProvider.Advance(TimeSpan.FromHours(2));

        await using (var context = _db.CreateContext())
        {
            var tracked = await context.Aggregates.SingleAsync(x => x.Id == aggregate.Id, TestContext.Current.CancellationToken);
            tracked.AddChild("Monday 08:00-12:00");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var loaded = await LoadAsync(aggregate.Id);
        loaded.Children.Count.ShouldBe(1);
        loaded.LastModifiedAtUtc.ShouldBe(TestDb.StartTime.AddHours(2).UtcDateTime);
    }

    [Fact]
    public async Task SaveChangesAsync_WithAnonymousUser_LeavesCreatedByNull()
    {
        _db.CurrentUser.UserId.Returns((Guid?)null);
        var aggregate = TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc);

        await AddAsync(aggregate);

        var loaded = await LoadAsync(aggregate.Id);
        loaded.CreatedAtUtc.ShouldBe(TestDb.StartTime.UtcDateTime);
        loaded.CreatedBy.ShouldBeNull();
    }

    public void Dispose() => _db.Dispose();

    private async Task AddAsync(TestAggregate aggregate)
    {
        await using var context = _db.CreateContext();
        context.Aggregates.Add(aggregate);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<TestAggregate> LoadAsync(TestAggregateId id)
    {
        await using var context = _db.CreateContext();
        return await context.Aggregates.SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);
    }
}
