using System.Security.Claims;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Users;
using Dispensario.Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Dispensario.Application.UnitTests.Authentication;

public sealed class CurrentUserTests
{
    [Fact]
    public void Properties_WithAuthenticatedPrincipal_ReturnClaimValues()
    {
        var userId = UserId.New();
        var doctorId = DoctorId.New();
        var currentUser = CreateCurrentUser(Authenticated(
            new Claim(JwtRegisteredClaimNames.Sub, userId.Value.ToString()),
            new Claim(CurrentUser.RoleClaimType, nameof(Role.Doctor)),
            new Claim(CurrentUser.DoctorIdClaimType, doctorId.Value.ToString())));

        currentUser.IsAuthenticated.ShouldBeTrue();
        currentUser.UserId.ShouldBe(userId);
        currentUser.Role.ShouldBe(Role.Doctor);
        currentUser.DoctorId.ShouldBe(doctorId);
    }

    [Fact]
    public void Properties_WithoutDoctorIdClaim_ReturnNullDoctorId()
    {
        var currentUser = CreateCurrentUser(Authenticated(
            new Claim(JwtRegisteredClaimNames.Sub, UserId.New().Value.ToString()),
            new Claim(CurrentUser.RoleClaimType, nameof(Role.Admin))));

        currentUser.Role.ShouldBe(Role.Admin);
        currentUser.DoctorId.ShouldBeNull();
    }

    [Fact]
    public void Properties_WithoutHttpContext_ReturnAnonymousUser()
    {
        var currentUser = new CurrentUser(new HttpContextAccessor());

        currentUser.IsAuthenticated.ShouldBeFalse();
        currentUser.UserId.ShouldBeNull();
        currentUser.Role.ShouldBeNull();
        currentUser.DoctorId.ShouldBeNull();
    }

    [Fact]
    public void Properties_WithUnauthenticatedPrincipal_ReturnAnonymousUser()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, UserId.New().Value.ToString())]));

        var currentUser = CreateCurrentUser(principal);

        currentUser.IsAuthenticated.ShouldBeFalse();
        currentUser.UserId.ShouldBeNull();
    }

    [Theory]
    [InlineData("Nurse")]
    [InlineData("42")]
    public void Role_WithUnknownRoleClaim_ReturnsNull(string role)
    {
        var currentUser = CreateCurrentUser(Authenticated(new Claim(CurrentUser.RoleClaimType, role)));

        currentUser.Role.ShouldBeNull();
    }

    [Fact]
    public void UserId_WithInvalidSubClaim_ReturnsNull()
    {
        var currentUser = CreateCurrentUser(Authenticated(new Claim(JwtRegisteredClaimNames.Sub, "not-a-guid")));

        currentUser.UserId.ShouldBeNull();
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    private static CurrentUser CreateCurrentUser(ClaimsPrincipal principal) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });
}
