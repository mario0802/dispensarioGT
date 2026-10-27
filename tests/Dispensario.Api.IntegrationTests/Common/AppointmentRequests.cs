using System.Globalization;
using System.Net.Http.Json;
using Dispensario.Domain.Appointments;
using Dispensario.Domain.Doctors;
using Dispensario.Domain.Patients;
using Dispensario.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using static Dispensario.Api.IntegrationTests.Common.DoctorRequests;
using static Dispensario.Api.IntegrationTests.Common.PatientRequests;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Common;

// Peticiones de citas compartidas por los tests de Appointments. Usan el reloj real:
// las fechas se calculan siempre a partir de mañana en la zona del dispensario
public static class AppointmentRequests
{
    public const string AppointmentsRoute = "/api/appointments";

    // Misma zona que appsettings.json (Clinic:TimeZone)
    private static readonly TimeZoneInfo ClinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil");

    // Fecha local del dispensario dentro de daysFromNow días (1 = mañana)
    public static DateOnly LocalDate(int daysFromNow = 1) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ClinicTimeZone)).AddDays(daysFromNow);

    // Hora local con el formato que espera la Api ("yyyy-MM-ddTHH:mm", sin offset)
    public static string LocalStart(int hour, int minute, int daysFromNow = 1) =>
        LocalDate(daysFromNow).ToDateTime(new TimeOnly(hour, minute)).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    // Doctor (citas de 30 min) con horario de lunes a domingo 08:00–18:00 y un paciente nuevos
    public static async Task<(Guid PatientId, Guid DoctorId)> CreatePatientAndDoctorAsync(HttpClient adminClient)
    {
        var doctorId = await CreateDoctorAsync(adminClient);
        await SetFullWeekScheduleAsync(adminClient, doctorId);
        var patientId = await CreatePatientAsync(adminClient);
        return (patientId, doctorId);
    }

    public static async Task SetFullWeekScheduleAsync(HttpClient adminClient, Guid doctorId)
    {
        var blocks = Enum.GetValues<DayOfWeek>()
            .Select(day => new { dayOfWeek = day.ToString(), startTime = "08:00", endTime = "18:00" })
            .ToList();

        var response = await adminClient.PutAsJsonAsync(
            $"{DoctorsRoute}/{doctorId}/schedule", new { blocks }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public static Dictionary<string, object?> NewAppointmentBody(Guid patientId, Guid doctorId, string startLocal) => new()
    {
        ["patientId"] = patientId,
        ["doctorId"] = doctorId,
        ["startLocal"] = startLocal,
        ["reason"] = "Control",
    };

    public static Task<HttpResponseMessage> PostAppointmentAsync(
        HttpClient client, Guid patientId, Guid doctorId, string startLocal) =>
        client.PostAsJsonAsync(
            AppointmentsRoute, NewAppointmentBody(patientId, doctorId, startLocal), TestContext.Current.CancellationToken);

    public static async Task<Guid> ScheduleAppointmentAsync(HttpClient client, Guid patientId, Guid doctorId, string startLocal)
    {
        var response = await PostAppointmentAsync(client, patientId, doctorId, startLocal);
        response.EnsureSuccessStatusCode();
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    // Inserta en la BD una cita que empezó hace una hora: la Api no deja agendar en el pasado.
    // Se crea con Appointment.Create y un nowUtc anterior a su inicio, así respeta las invariantes del aggregate
    public static async Task<Guid> SeedStartedAppointmentAsync(DispensarioApiFactory factory, Guid patientId, Guid doctorId)
    {
        var startUtc = DateTime.UtcNow.AddHours(-1);
        var appointment = Appointment.Create(
            new PatientId(patientId),
            new DoctorId(doctorId),
            startUtc,
            startUtc.AddMinutes(30),
            "Control",
            startUtc.AddDays(-1));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DispensarioDbContext>();
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return appointment.Id.Value;
    }
}
