using BuildingBlocks.Domain;
using Dispensario.Domain.Common;

namespace Dispensario.Domain.Users.Events;

public sealed record UserCreatedDomainEvent(UserId UserId, Email Email, Role Role) : IDomainEvent;
