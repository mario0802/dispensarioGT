using System.Net.Http.Json;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Common;

// Peticiones de doctores compartidas por los tests de Doctors y Users
public static class DoctorRequests
{
    public const string DoctorsRoute = "/api/doctors";

    // Matrícula única por llamada: los tests comparten la base de datos
    public static Dictionary<string, object?> NewDoctorBody(int? appointmentDurationMinutes = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["firstName"] = "Luis",
            ["lastName"] = "Mora",
            ["licenseNumber"] = $"mp-{Guid.NewGuid().ToString("N")[..12]}",
            ["specialty"] = "Pediatria",
            ["email"] = "luis.mora@dispensario.local",
            ["phone"] = "0991234567",
        };

        if (appointmentDurationMinutes is not null)
            body["appointmentDurationMinutes"] = appointmentDurationMinutes;

        return body;
    }

    public static async Task<Guid> CreateDoctorAsync(HttpClient adminClient, Dictionary<string, object?>? body = null)
    {
        var response = await adminClient.PostAsJsonAsync(DoctorsRoute, body ?? NewDoctorBody(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }
}
