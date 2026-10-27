using System.Net.Http.Json;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Common;

// Peticiones de pacientes compartidas por los tests de Patients
public static class PatientRequests
{
    public const string PatientsRoute = "/api/patients";

    // Documento único por llamada: los tests comparten la base de datos
    public static Dictionary<string, object?> NewPatientBody()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        return new Dictionary<string, object?>
        {
            ["documentType"] = "NationalId",
            ["documentNumber"] = suffix,
            ["firstName"] = "Ana",
            ["lastName"] = $"Perez-{suffix}",
            ["birthDate"] = "1990-05-14",
            ["gender"] = "Female",
            ["email"] = "ana@dispensario.local",
            ["phone"] = "0991234567",
            ["address"] = new Dictionary<string, object?>
            {
                ["street"] = "Av. Amazonas 123",
                ["city"] = "Quito",
                ["state"] = null,
                ["reference"] = null,
            },
            ["bloodType"] = "O+",
            ["allergies"] = "Penicillin",
        };
    }

    public static async Task<Guid> CreatePatientAsync(HttpClient client, Dictionary<string, object?>? body = null)
    {
        var response = await client.PostAsJsonAsync(PatientsRoute, body ?? NewPatientBody(), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }
}
