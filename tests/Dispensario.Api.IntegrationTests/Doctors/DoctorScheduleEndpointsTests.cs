using System.Net;
using System.Net.Http.Json;
using Dispensario.Api.IntegrationTests.Common;
using Dispensario.Domain.Users;
using static Dispensario.Api.IntegrationTests.Common.DoctorRequests;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Doctors;

public sealed class DoctorScheduleEndpointsTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    private static readonly object[] MondayMorning = [Block("Monday", "09:00", "12:00")];

    [Fact]
    public async Task SetDoctorSchedule_AsAdmin_ReturnsNoContentAndGetReturnsOrderedBlocks()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);

        var response = await PutScheduleAsync(admin, doctorId,
            Block("Sunday", "10:00", "12:00"),
            Block("Monday", "14:00", "18:00"),
            Block("Monday", "09:00", "12:00"),
            Block("Wednesday", "08:30", "09:30"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetBlocksAsync(admin, doctorId)).ShouldBe(
        [
            "Monday 09:00-12:00",
            "Monday 14:00-18:00",
            "Wednesday 08:30-09:30",
            "Sunday 10:00-12:00",
        ]);
    }

    [Fact]
    public async Task SetDoctorSchedule_Twice_ReplacesWholeSchedule()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);
        (await PutScheduleAsync(admin, doctorId, MondayMorning)).EnsureSuccessStatusCode();

        (await PutScheduleAsync(admin, doctorId, Block("Friday", "15:00", "17:00"))).EnsureSuccessStatusCode();

        (await GetBlocksAsync(admin, doctorId)).ShouldBe(["Friday 15:00-17:00"]);
    }

    [Fact]
    public async Task SetDoctorSchedule_WithEmptyList_ClearsSchedule()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);
        (await PutScheduleAsync(admin, doctorId, MondayMorning)).EnsureSuccessStatusCode();

        var response = await PutScheduleAsync(admin, doctorId);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetBlocksAsync(admin, doctorId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SetDoctorSchedule_WithContiguousBlocks_ReturnsNoContent()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);

        var response = await PutScheduleAsync(admin, doctorId, Block("Monday", "09:00", "12:00"), Block("Monday", "12:00", "14:00"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("11:00", "13:00")]   // solape
    [InlineData("14:00", "14:20")]   // más corto que la duración (30)
    [InlineData("14:03", "15:00")]   // minutos que no son múltiplo de 5
    [InlineData("15:00", "14:00")]   // inicio posterior al fin
    public async Task SetDoctorSchedule_BreakingScheduleRule_ReturnsBadRequestProblem(string startTime, string endTime)
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);

        var response = await PutScheduleAsync(admin, doctorId, Block("Monday", "09:00", "12:00"), Block("Monday", startTime, endTime));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData("Monday", "9:00")]
    [InlineData("Monday", "09:00:00")]
    [InlineData("Funday", "09:00")]
    public async Task SetDoctorSchedule_WithInvalidFormat_ReturnsBadRequestProblem(string dayOfWeek, string startTime)
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);

        var response = await PutScheduleAsync(admin, doctorId, Block(dayOfWeek, startTime, "12:00"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task SetDoctorSchedule_WithBadTimeFormat_ReturnsValidationErrors()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);

        var response = await PutScheduleAsync(admin, doctorId, Block("Monday", "9:00", "12:00"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("ValidationErrors").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SetDoctorSchedule_WithUnknownDoctor_ReturnsNotFoundProblem()
    {
        var admin = await AdminAsync();

        var response = await PutScheduleAsync(admin, Guid.NewGuid(), MondayMorning);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task SetDoctorSchedule_WithInactiveDoctor_ReturnsBadRequestButGetStillWorks()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);
        (await admin.PostAsync($"{DoctorsRoute}/{doctorId}/deactivate", null, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        var response = await PutScheduleAsync(admin, doctorId, MondayMorning);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.GetAsync(ScheduleRoute(doctorId), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateDoctor_WithDurationLongerThanScheduleBlock_ReturnsBadRequestAndKeepsDoctor()
    {
        var admin = await AdminAsync();
        var body = NewDoctorBody();
        var doctorId = await CreateDoctorAsync(admin, body);
        (await PutScheduleAsync(admin, doctorId, Block("Monday", "09:00", "09:30"))).EnsureSuccessStatusCode();
        body["appointmentDurationMinutes"] = 45;
        body["specialty"] = "Cardiologia";

        var response = await admin.PutAsJsonAsync($"{DoctorsRoute}/{doctorId}", body, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var doctor = await ReadJsonAsync(await admin.GetAsync($"{DoctorsRoute}/{doctorId}", TestContext.Current.CancellationToken));
        doctor.RootElement.GetProperty("appointmentDurationMinutes").GetInt32().ShouldBe(30);
        doctor.RootElement.GetProperty("specialty").GetString().ShouldBe("Pediatria");
    }

    [Fact]
    public async Task SetDoctorSchedule_AsLinkedDoctorOnOwnDoctor_ReturnsNoContent()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);
        var doctorClient = await AuthenticateAsLinkedDoctorAsync(factory, admin, doctorId);

        var response = await PutScheduleAsync(doctorClient, doctorId, MondayMorning);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetBlocksAsync(doctorClient, doctorId)).ShouldBe(["Monday 09:00-12:00"]);
    }

    [Fact]
    public async Task SetDoctorSchedule_AsLinkedDoctorOnOtherDoctor_ReturnsForbiddenProblem()
    {
        var admin = await AdminAsync();
        var doctorClient = await AuthenticateAsLinkedDoctorAsync(factory, admin, await CreateDoctorAsync(admin));
        var otherDoctorId = await CreateDoctorAsync(admin);

        var response = await PutScheduleAsync(doctorClient, otherDoctorId, MondayMorning);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task SetDoctorSchedule_AsLinkedDoctorOnUnknownDoctor_ReturnsForbiddenProblem()
    {
        var admin = await AdminAsync();
        var doctorClient = await AuthenticateAsLinkedDoctorAsync(factory, admin, await CreateDoctorAsync(admin));

        var response = await PutScheduleAsync(doctorClient, Guid.NewGuid(), MondayMorning);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetDoctorSchedule_AsUnlinkedDoctor_ReturnsForbiddenProblem()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);
        var doctorClient = await factory.AuthenticateAsAsync(Role.Doctor, TestContext.Current.CancellationToken);

        var response = await PutScheduleAsync(doctorClient, doctorId, MondayMorning);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetDoctorSchedule_AsReceptionist_ReturnsForbiddenProblem()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);
        var receptionist = await factory.AuthenticateAsAsync(Role.Receptionist, TestContext.Current.CancellationToken);

        var response = await PutScheduleAsync(receptionist, doctorId, MondayMorning);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Receptionist)]
    [InlineData(Role.Doctor)]
    public async Task GetDoctorSchedule_AsAnyRole_ReturnsOk(Role role)
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);
        (await PutScheduleAsync(admin, doctorId, MondayMorning)).EnsureSuccessStatusCode();
        var client = await factory.AuthenticateAsAsync(role, TestContext.Current.CancellationToken);

        var response = await client.GetAsync(ScheduleRoute(doctorId), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("doctorId").GetGuid().ShouldBe(doctorId);
        (await GetBlocksAsync(client, doctorId)).ShouldBe(["Monday 09:00-12:00"]);
    }

    [Fact]
    public async Task GetDoctorSchedule_WithoutSchedule_ReturnsEmptyBlocks()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);

        (await GetBlocksAsync(admin, doctorId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDoctorSchedule_WithUnknownDoctor_ReturnsNotFoundProblem()
    {
        var admin = await AdminAsync();

        var response = await admin.GetAsync(ScheduleRoute(Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task ScheduleEndpoints_WithoutToken_ReturnUnauthorizedProblem()
    {
        var client = factory.CreateClient();
        var doctorId = Guid.NewGuid();

        var put = await PutScheduleAsync(client, doctorId, MondayMorning);
        var get = await client.GetAsync(ScheduleRoute(doctorId), TestContext.Current.CancellationToken);

        put.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        get.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        put.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        get.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    private Task<HttpClient> AdminAsync() => factory.AuthenticateAsAsync(Role.Admin, TestContext.Current.CancellationToken);

    private static string ScheduleRoute(Guid doctorId) => $"{DoctorsRoute}/{doctorId}/schedule";

    private static object Block(string dayOfWeek, string startTime, string endTime) => new { dayOfWeek, startTime, endTime };

    private static Task<HttpResponseMessage> PutScheduleAsync(HttpClient client, Guid doctorId, params object[] blocks) =>
        client.PutAsJsonAsync(ScheduleRoute(doctorId), new { blocks }, TestContext.Current.CancellationToken);

    // Cada bloque como "Day HH:mm-HH:mm", en el orden en que lo devuelve la API
    private static async Task<List<string>> GetBlocksAsync(HttpClient client, Guid doctorId)
    {
        var response = await client.GetAsync(ScheduleRoute(doctorId), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("blocks").EnumerateArray()
            .Select(block =>
                $"{block.GetProperty("dayOfWeek").GetString()} {block.GetProperty("startTime").GetString()}-{block.GetProperty("endTime").GetString()}")
            .ToList();
    }
}
