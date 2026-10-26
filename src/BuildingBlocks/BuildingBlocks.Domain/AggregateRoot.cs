namespace BuildingBlocks.Domain;

// Raíz de consistencia: acumula los domain events hasta que la infraestructura los despacha
public abstract class AggregateRoot<TId> : Entity<TId>, IAuditable, IHasDomainEvents where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    // Auditoría: setters públicos porque los escribe el interceptor, fuera del aggregate
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }
}
