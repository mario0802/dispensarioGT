namespace BuildingBlocks.Domain;

// Vista no genérica de AggregateRoot<TId>: el interceptor lee los eventos sin conocer el TId
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
