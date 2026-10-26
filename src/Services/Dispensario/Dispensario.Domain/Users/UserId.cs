using BuildingBlocks.Domain;

namespace Dispensario.Domain.Users;

public readonly record struct UserId(Guid Value) : IStronglyTypedId
{
    public static UserId New() => new(Guid.CreateVersion7());
}
