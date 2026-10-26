using BuildingBlocks.Domain;
using Dispensario.Application.UnitTests.Persistence.Fakes;
using Dispensario.Domain.Common;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Dispensario.Application.UnitTests.Persistence;

public sealed class DispatchDomainEventsInterceptorTests : IDisposable
{
    private static readonly DateTime ScheduledAtUtc = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);

    private readonly TestDb _db = new();

    [Fact]
    public async Task SaveChangesAsync_WithEvents_PublishesInOrderAndClearsThem()
    {
        var published = new List<IDomainEvent>();
        _db.Publisher
            .When(x => x.Publish(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>()))
            .Do(call => published.Add(call.Arg<IDomainEvent>()));
        var aggregate = TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc);
        aggregate.RaiseTestEvent(1);
        aggregate.RaiseTestEvent(2);

        await using var context = _db.CreateContext();
        context.Aggregates.Add(aggregate);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        published.ShouldBe([new TestDomainEvent(aggregate.Id, 1), new TestDomainEvent(aggregate.Id, 2)]);
        aggregate.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_WithoutEvents_DoesNotPublish()
    {
        var aggregate = TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc);

        await using var context = _db.CreateContext();
        context.Aggregates.Add(aggregate);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _db.Publisher.DidNotReceive().Publish(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SaveChanges_WithPendingEvents_ThrowsInvalidOperationException()
    {
        var aggregate = TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc);
        aggregate.RaiseTestEvent(1);

        using var context = _db.CreateContext();
        context.Aggregates.Add(aggregate);

        var exception = Should.Throw<InvalidOperationException>(() => context.SaveChanges());
        exception.Message.ShouldBe("Domain events can only be dispatched with SaveChangesAsync.");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenSaveFails_DoesNotPublishAndKeepsEvents()
    {
        var id = TestAggregateId.New();
        await using (var context = _db.CreateContext())
        {
            context.Aggregates.Add(TestAggregate.Create(FullName.Create("Ana", "Pérez"), ScheduledAtUtc, id: id));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var duplicate = TestAggregate.Create(FullName.Create("Luis", "Gómez"), ScheduledAtUtc, id: id);
        duplicate.RaiseTestEvent(1);

        await using var failingContext = _db.CreateContext();
        failingContext.Aggregates.Add(duplicate);

        await Should.ThrowAsync<DbUpdateException>(
            () => failingContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        await _db.Publisher.DidNotReceive().Publish(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
        duplicate.DomainEvents.Count.ShouldBe(1);
    }

    public void Dispose() => _db.Dispose();
}
