using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dispensario.Api.IntegrationTests.Common;
using Dispensario.Domain.Users;
using static Dispensario.Api.IntegrationTests.Common.AppointmentRequests;
using static Dispensario.Api.IntegrationTests.Common.DoctorRequests;
using static Dispensario.Api.IntegrationTests.Common.PatientRequests;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Appointments;

// Usan el reloj real: el doctor solo atiende, de 09:00 a 11:00, el día de la semana que cae dentro de 8 días
public sealed class AvailableSlotsEndpointsTests(DispensarioApiFactory factory) : IClassFixture<DispensarioApiFactory>
{
    private const int DaysAhead = 8;

    [Fact]
    public async Task GetAvailableSlots_DayWithSchedule_ReturnsAlignedSlots()
    {
        var reception = await ReceptionAsync();
        var doctorId = await CreateDoctorWithScheduleAsync();

        var response = await GetSlotsAsync(reception, doctorId, LocalDate(DaysAhead));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("doctorId").GetGuid().ShouldBe(doctorId);
        json.RootElement.GetProperty("date").GetString().ShouldBe(FormatDate(LocalDate(DaysAhead)));
        json.RootElement.GetProperty("durationMinutes").GetInt32().ShouldBe(30);
        StartTimes(json).ShouldBe(["09:00", "09:30", "10:00", "10:30"]);
        json.RootElement.GetProperty("slots")[0].GetProperty("startLocal").GetString()
            .ShouldBe($"{FormatDate(LocalDate(DaysAhead))}T09:00:00");
        json.RootElement.GetProperty("slots")[0].GetProperty("endLocal").GetString()
            .ShouldBe($"{FormatDate(LocalDate(DaysAhead))}T09:30:00");
    }

    [Fact]
    public async Task GetAvailableSlots_AfterSchedulingAndCancelling_HidesAndRestoresSlot()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorWithScheduleAsync();
        var patientId = await CreatePatientAsync(admin);

        // Agenda un slot devuelto por el propio endpoint
        var appointmentId = await ScheduleAppointmentAsync(admin, patientId, doctorId, LocalStart(9, 30, DaysAhead));

        using (var afterScheduling = await ReadJsonAsync(await GetSlotsAsync(admin, doctorId, LocalDate(DaysAhead))))
            StartTimes(afterScheduling).ShouldBe(["09:00", "10:00", "10:30"]);

        var cancel = await admin.PostAsJsonAsync(
            $"{AppointmentsRoute}/{appointmentId}/cancel", new { reason = "Viaje" }, TestContext.Current.CancellationToken);
        cancel.EnsureSuccessStatusCode();

        using var afterCancelling = await ReadJsonAsync(await GetSlotsAsync(admin, doctorId, LocalDate(DaysAhead)));
        StartTimes(afterCancelling).ShouldBe(["09:00", "09:30", "10:00", "10:30"]);
    }

    [Fact]
    public async Task GetAvailableSlots_DayWithoutSchedule_ReturnsEmptySlots()
    {
        var reception = await ReceptionAsync();
        var doctorId = await CreateDoctorWithScheduleAsync();

        var response = await GetSlotsAsync(reception, doctorId, LocalDate(DaysAhead + 1));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        json.RootElement.GetProperty("slots").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetAvailableSlots_WithPastDate_ReturnsBadRequestProblem()
    {
        var reception = await ReceptionAsync();
        var doctorId = await CreateDoctorWithScheduleAsync();

        var response = await GetSlotsAsync(reception, doctorId, LocalDate(-1));

        ShouldBeProblem(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?date=not-a-date")]
    public async Task GetAvailableSlots_WithMissingOrMalformedDate_ReturnsBadRequestProblem(string queryString)
    {
        var reception = await ReceptionAsync();
        var doctorId = await CreateDoctorWithScheduleAsync();

        var response = await reception.GetAsync(
            $"{DoctorsRoute}/{doctorId}/available-slots{queryString}", TestContext.Current.CancellationToken);

        ShouldBeProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAvailableSlots_WithInactiveDoctor_ReturnsBadRequestProblem()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorWithScheduleAsync();
        (await admin.PostAsync($"{DoctorsRoute}/{doctorId}/deactivate", null, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();

        var response = await GetSlotsAsync(admin, doctorId, LocalDate(DaysAhead));

        ShouldBeProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAvailableSlots_WithUnknownDoctor_ReturnsNotFoundProblem()
    {
        var reception = await ReceptionAsync();

        var response = await GetSlotsAsync(reception, Guid.CreateVersion7(), LocalDate(DaysAhead));

        ShouldBeProblem(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAvailableSlots_AsUnlinkedDoctor_ReturnsOk()
    {
        var doctorId = await CreateDoctorWithScheduleAsync();
        var doctor = await factory.AuthenticateAsAsync(Role.Doctor, TestContext.Current.CancellationToken);

        var response = await GetSlotsAsync(doctor, doctorId, LocalDate(DaysAhead));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAvailableSlots_WithoutToken_ReturnsUnauthorized()
    {
        var doctorId = await CreateDoctorWithScheduleAsync();

        var response = await GetSlotsAsync(factory.CreateClient(), doctorId, LocalDate(DaysAhead));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Task<HttpResponseMessage> GetSlotsAsync(HttpClient client, Guid doctorId, DateOnly date) =>
        client.GetAsync(
            $"{DoctorsRoute}/{doctorId}/available-slots?date={FormatDate(date)}", TestContext.Current.CancellationToken);

    private static List<string> StartTimes(JsonDocument json) =>
        json.RootElement.GetProperty("slots").EnumerateArray()
            .Select(slot => slot.GetProperty("startLocal").GetString()![11..16])
            .ToList();

    private static void ShouldBeProblem(HttpResponseMessage response, HttpStatusCode statusCode)
    {
        response.StatusCode.ShouldBe(statusCode);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    // Doctor nuevo (citas de 30 min) que solo atiende de 09:00 a 11:00 el día de la semana de LocalDate(DaysAhead)
    private async Task<Guid> CreateDoctorWithScheduleAsync()
    {
        var admin = await AdminAsync();
        var doctorId = await CreateDoctorAsync(admin);
        var blocks = new[] { new { dayOfWeek = LocalDate(DaysAhead).DayOfWeek.ToString(), startTime = "09:00", endTime = "11:00" } };

        var response = await admin.PutAsJsonAsync(
            $"{DoctorsRoute}/{doctorId}/schedule", new { blocks }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return doctorId;
    }

    private Task<HttpClient> AdminAsync() => factory.AuthenticateAsAsync(Role.Admin, TestContext.Current.CancellationToken);

    private Task<HttpClient> ReceptionAsync() => factory.AuthenticateAsAsync(Role.Receptionist, TestContext.Current.CancellationToken);
}
