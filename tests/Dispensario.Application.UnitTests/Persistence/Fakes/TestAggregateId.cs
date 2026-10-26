using BuildingBlocks.Domain;

namespace Dispensario.Application.UnitTests.Persistence.Fakes;

public readonly record struct TestAggregateId(Guid Value) : IStronglyTypedId
{
    public static TestAggregateId New() => new(Guid.CreateVersion7());
}
