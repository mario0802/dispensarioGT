using BuildingBlocks.Pagination;
using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Users.Queries.GetUsers;
using Dispensario.Domain.Users;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Users;

public sealed record GetUsersRequest(
    int PageIndex = 0,
    int PageSize = 10,
    string? Search = null,
    Role? Role = null,
    bool? IsActive = null);

/// <summary>Lista los usuarios paginados, con búsqueda y filtros por rol y estado.</summary>
public sealed class GetUsersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users", async ([AsParameters] GetUsersRequest request, ISender sender, CancellationToken ct) =>
        {
            var query = new GetUsersQuery(
                new PaginationRequest(request.PageIndex, request.PageSize),
                request.Search,
                request.Role,
                request.IsActive);
            var result = await sender.Send(query, ct);
            return Results.Ok(new PaginatedResult<UserResponse>(
                result.PageIndex,
                result.PageSize,
                result.Count,
                result.Data.Adapt<List<UserResponse>>()));
        })
        .WithName("GetUsers")
        .WithTags("Users")
        .WithSummary("Get users")
        .Produces<PaginatedResult<UserResponse>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .RequireAuthorization(Policies.AdminOnly);
    }
}
