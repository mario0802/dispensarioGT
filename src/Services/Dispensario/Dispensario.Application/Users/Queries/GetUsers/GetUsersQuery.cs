using BuildingBlocks.CQRS;
using BuildingBlocks.Pagination;
using Dispensario.Application.Users.Dtos;
using Dispensario.Domain.Users;

namespace Dispensario.Application.Users.Queries.GetUsers;

public sealed record GetUsersQuery(PaginationRequest Pagination, string? Search, Role? Role, bool? IsActive)
    : IQuery<PaginatedResult<UserDto>>;
