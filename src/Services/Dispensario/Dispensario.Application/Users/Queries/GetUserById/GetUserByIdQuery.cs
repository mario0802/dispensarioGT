using BuildingBlocks.CQRS;
using Dispensario.Application.Users.Dtos;

namespace Dispensario.Application.Users.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserDto>;
