using MediatR;

namespace BuildingBlocks.Domain;

// Marcador de un hecho de negocio ocurrido dentro de un aggregate; se publica después del commit
public interface IDomainEvent : INotification;
