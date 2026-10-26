using Carter;
using Dispensario.Application.Users.Queries.GetCurrentUser;
using Dispensario.Domain.Common;
using Dispensario.Domain.Users;
using Mapster;
using MediatR;

namespace Dispensario.Api.Endpoints.Auth;

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    DocumentType IdentificationType,
    string IdentificationNumber,
    string? Phone,
    Role Role,
    Guid? DoctorId,
    bool IsActive);

/// <summary>Devuelve los datos del usuario autenticado.</summary>
public sealed class GetCurrentUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/me", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCurrentUserQuery(), ct);
            return Results.Ok(result.Adapt<CurrentUserResponse>());
        })
        .WithName("GetCurrentUser")
        .WithTags("Auth")
        .WithSummary("Get current user")
        .Produces<CurrentUserResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();
    }
}
