using BuildingBlocks.CQRS;
using Dispensario.Application.Users.Dtos;

namespace Dispensario.Application.Users.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery : IQuery<CurrentUserDto>;
