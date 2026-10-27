using System.Net.Http.Json;
using static Dispensario.Api.IntegrationTests.Common.PatientRequests;
using static Dispensario.Api.IntegrationTests.Common.UserRequests;

namespace Dispensario.Api.IntegrationTests.Common;

// Peticiones de consultas compartidas por los tests de Consultations
public static class ConsultationRequests
{
    public const string ConsultationsRoute = "/api/consultations";

    public static Dictionary<string, object?> NewConsultationBody(Guid patientId, Guid? appointmentId = null) => new()
    {
        ["patientId"] = patientId,
        ["appointmentId"] = appointmentId,
        ["reason"] = "Dolor de cabeza desde hace tres días",
    };

    public static Task<HttpResponseMessage> PostConsultationAsync(HttpClient client, Dictionary<string, object?> body) =>
        client.PostAsJsonAsync(ConsultationsRoute, body, TestContext.Current.CancellationToken);

    // Inicia una consulta y devuelve su ID
    public static async Task<Guid> StartConsultationAsync(HttpClient doctorClient, Guid patientId, Guid? appointmentId = null)
    {
        var response = await PostConsultationAsync(doctorClient, NewConsultationBody(patientId, appointmentId));
        response.EnsureSuccessStatusCode();
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> UpdateConsultationAsync(
        HttpClient client, Guid id, string reason, string? diagnosis, string? notes) =>
        client.PutAsJsonAsync($"{ConsultationsRoute}/{id}", new { reason, diagnosis, notes }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> CloseConsultationAsync(HttpClient client, Guid id, string diagnosis, string? notes = null) =>
        client.PostAsJsonAsync($"{ConsultationsRoute}/{id}/close", new { diagnosis, notes }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> GetConsultationAsync(HttpClient client, Guid id) =>
        client.GetAsync($"{ConsultationsRoute}/{id}", TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> GetPatientConsultationsAsync(HttpClient client, Guid patientId, string query = "") =>
        client.GetAsync($"{PatientsRoute}/{patientId}/consultations{query}", TestContext.Current.CancellationToken);
}
