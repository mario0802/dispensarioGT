using BuildingBlocks.Domain;

namespace Dispensario.Application.UnitTests.Persistence.Fakes;

// Number distingue dos eventos del mismo aggregate al comprobar el orden de publicación
public sealed record TestDomainEvent(TestAggregateId Id, int Number) : IDomainEvent;
