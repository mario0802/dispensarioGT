using BuildingBlocks.Domain;
using Dispensario.Domain.Common;

namespace Dispensario.Application.UnitTests.Persistence.Fakes;

// Aggregate de prueba con los tipos que usarán los módulos: ID tipado, VOs complejos, fechas y entidades propias
public sealed class TestAggregate : AggregateRoot<TestAggregateId>
{
    private readonly List<TestChild> _children = [];

    // Constructor para EF: Name lo materializa EF justo después
    private TestAggregate()
    {
        Name = null!;
    }

    private TestAggregate(TestAggregateId id, FullName name, DateTime scheduledAtUtc, Address? address, TestAggregateId? parentId)
    {
        Id = id;
        Name = name;
        ScheduledAtUtc = scheduledAtUtc;
        Address = address;
        ParentId = parentId;
    }

    public FullName Name { get; private set; }
    public Address? Address { get; private set; }
    public TestAggregateId? ParentId { get; private set; }
    public DateTime ScheduledAtUtc { get; private set; }
    public IReadOnlyCollection<TestChild> Children => _children.AsReadOnly();

    public static TestAggregate Create(
        FullName name,
        DateTime scheduledAtUtc,
        Address? address = null,
        TestAggregateId? parentId = null,
        TestAggregateId? id = null)
        => new(id ?? TestAggregateId.New(), name, scheduledAtUtc, address, parentId);

    public void Rename(FullName name) => Name = name;

    public void AddChild(string description) => _children.Add(new TestChild(description));

    public void RaiseTestEvent(int number) => Raise(new TestDomainEvent(Id, number));
}
