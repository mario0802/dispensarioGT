using System.Net.Http.Headers;
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

    /// <summary>
    /// Crea un usuario Doctor, lo vincula al doctor y devuelve un cliente con un token emitido tras el vínculo
    /// (el claim doctor_id solo llega en los tokens nuevos).
    /// </summary>
    public static async Task<HttpClient> AuthenticateAsLinkedDoctorAsync(
        DispensarioApiFactory factory, HttpClient adminClient, Guid doctorId)
    {
        var userBody = NewUserBody("Doctor");
        var userId = await CreateUserAsync(adminClient, "Doctor", userBody);
        var link = await adminClient.PostAsJsonAsync(
            $"/api/users/{userId}/link-doctor", new { doctorId }, TestContext.Current.CancellationToken);
        link.EnsureSuccessStatusCode();

        var client = factory.CreateClient();
        var login = await LoginAsync(client, userBody["email"]!, Password);
        login.EnsureSuccessStatusCode();
        using var json = await ReadJsonAsync(login);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString());
        return client;
    }
}
