using BuildingBlocks.Domain;

namespace BuildingBlocks.UnitTests.Domain;

public sealed class AggregateRootTests
{
    [Fact]
    public void Raise_TwoEvents_KeepsThemInOrder()
    {
        var aggregate = new TestAggregate();
        var first = new TestEvent(1);
        var second = new TestEvent(2);

        aggregate.DoSomething(first);
        aggregate.DoSomething(second);

        aggregate.DomainEvents.ShouldBe([first, second]);
    }

    [Fact]
    public void ClearDomainEvents_WithEvents_LeavesListEmpty()
    {
        var aggregate = new TestAggregate();
        aggregate.DoSomething(new TestEvent(1));
        aggregate.DoSomething(new TestEvent(2));

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void DomainEvents_CastToList_IsNotPossible()
    {
        var aggregate = new TestAggregate();
        aggregate.DoSomething(new TestEvent(1));

        var result = aggregate.DomainEvents as List<IDomainEvent>;

        result.ShouldBeNull();
    }

    [Fact]
    public void AggregateRoot_Always_ImplementsAuditable()
    {
        var aggregate = new TestAggregate();
        var createdAtUtc = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        IAuditable auditable = aggregate;
        auditable.CreatedAtUtc = createdAtUtc;
        auditable.CreatedBy = "admin";

        aggregate.CreatedAtUtc.ShouldBe(createdAtUtc);
        aggregate.CreatedBy.ShouldBe("admin");
        aggregate.LastModifiedAtUtc.ShouldBeNull();
        aggregate.LastModifiedBy.ShouldBeNull();
    }

    private sealed record TestEvent(int Number) : IDomainEvent;

    private sealed class TestAggregate : AggregateRoot<Guid>
    {
        public TestAggregate() => Id = Guid.CreateVersion7();

        public void DoSomething(IDomainEvent domainEvent) => Raise(domainEvent);
    }
}
