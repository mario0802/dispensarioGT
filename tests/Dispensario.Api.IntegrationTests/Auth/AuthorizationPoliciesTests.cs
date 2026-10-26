using Dispensario.Api.Authorization;
using Dispensario.Api.IntegrationTests.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dispensario.Api.IntegrationTests.Auth;

public sealed class AuthorizationPoliciesTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    [Theory]
    [InlineData(Policies.AdminOnly, new[] { "Admin" })]
    [InlineData(Policies.Reception, new[] { "Admin", "Receptionist" })]
    [InlineData(Policies.Clinical, new[] { "Doctor" })]
    public async Task GetPolicyAsync_RegisteredPolicy_RequiresExpectedRoles(string policyName, string[] roles)
    {
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await provider.GetPolicyAsync(policyName);

        policy.ShouldNotBeNull();
        var requirement = policy.Requirements.OfType<RolesAuthorizationRequirement>().ShouldHaveSingleItem();
        requirement.AllowedRoles.ShouldBe(roles, ignoreOrder: true);
    }
}
