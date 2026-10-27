using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dispensario.Api.IntegrationTests.Common;
using Dispensario.Domain.Users;
using static Dispensario.Api.IntegrationTests.Common.DoctorRequests;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Users;

public sealed class LinkDoctorEndpointTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    [Fact]
    public async Task LinkDoctor_AsAdmin_LinksUserAndAddsDoctorIdAfterNewLogin()
    {
        var admin = await AdminAsync();
        var userBody = NewUserBody("Doctor");
        var userId = await CreateUserAsync(admin, "Doctor", userBody);
        var doctorId = await CreateDoctorAsync(admin);

        var response = await LinkAsync(admin, userId, doctorId);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using (var user = await ReadJsonAsync(await admin.GetAsync($"/api/users/{userId}", TestContext.Current.CancellationToken)))
            user.RootElement.GetProperty("doctorId").GetGuid().ShouldBe(doctorId);
        using (var doctor = await ReadJsonAsync(await admin.GetAsync($"{DoctorsRoute}/{doctorId}", TestContext.Current.CancellationToken)))
            doctor.RootElement.GetProperty("userId").GetGuid().ShouldBe(userId);

        // El claim doctor_id solo llega en los tokens emitidos después del vínculo
        var client = factory.CreateClient();
        using var login = await ReadJsonAsync(await LoginAsync(client, userBody["email"]!, Password));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.RootElement.GetProperty("accessToken").GetString());
        using var me = await ReadJsonAsync(await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken));
        me.RootElement.GetProperty("doctorId").GetGuid().ShouldBe(doctorId);
    }

    [Fact]
    public async Task LinkDoctor_WithDoctorLinkedToOtherUser_ReturnsConflictProblem()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);
        (await LinkAsync(admin, await CreateUserAsync(admin, "Doctor"), doctorId)).EnsureSuccessStatusCode();

        var response = await LinkAsync(admin, await CreateUserAsync(admin, "Doctor"), doctorId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task LinkDoctor_WithReceptionistUser_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();

        var response = await LinkAsync(admin, await CreateUserAsync(admin, "Receptionist"), await CreateDoctorAsync(admin));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task LinkDoctor_WithAlreadyLinkedUser_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();
        var userId = await CreateUserAsync(admin, "Doctor");
        (await LinkAsync(admin, userId, await CreateDoctorAsync(admin))).EnsureSuccessStatusCode();

        var response = await LinkAsync(admin, userId, await CreateDoctorAsync(admin));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LinkDoctor_WithInactiveDoctor_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);
        (await admin.PostAsync($"{DoctorsRoute}/{doctorId}/deactivate", null, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var response = await LinkAsync(admin, await CreateUserAsync(admin, "Doctor"), doctorId);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LinkDoctor_WithUnknownDoctor_ReturnsNotFoundProblem()
    {
        var admin = await AdminAsync();

        var response = await LinkAsync(admin, await CreateUserAsync(admin, "Doctor"), Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkDoctor_WithUnknownUser_ReturnsNotFoundProblem()
    {
        var admin = await AdminAsync();

        var response = await LinkAsync(admin, Guid.NewGuid(), await CreateDoctorAsync(admin));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(Role.Receptionist)]
    [InlineData(Role.Doctor)]
    public async Task LinkDoctor_AsNonAdmin_ReturnsForbiddenProblem(Role role)
    {
        var client = await factory.AuthenticateAsAsync(role, TestContext.Current.CancellationToken);

        var response = await LinkAsync(client, Guid.NewGuid(), Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task LinkDoctor_WithoutToken_ReturnsUnauthorizedProblem()
    {
        var response = await LinkAsync(factory.CreateClient(), Guid.NewGuid(), Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    private Task<HttpClient> AdminAsync() => factory.AuthenticateAsAsync(Role.Admin, TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> LinkAsync(HttpClient client, Guid userId, Guid doctorId) =>
        client.PostAsJsonAsync($"/api/users/{userId}/link-doctor", new { doctorId }, TestContext.Current.CancellationToken);
}
