using Carter;
using Dispensario.Api.Authorization;
using Dispensario.Application.Users.Queries.GetUserById;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Users;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    DocumentType IdentificationType,
    string IdentificationNumber,
    string? Phone,
    Role Role,
    Guid? DoctorId,
    bool IsActive,
    DateTime CreatedAtUtc);

/// <summary>Devuelve un usuario por su ID.</summary>
public sealed class GetUserByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUserByIdQuery(id), ct);
            return Results.Ok(result.Adapt<UserResponse>());
        })
        .WithName("GetUserById")
        .WithTags("Users")
        .WithSummary("Get user by id")
        .Produces<UserResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization(Policies.AdminOnly);
    }
}
