using System.Net.Http.Json;
using System.Text.Json;

namespace Dispensario.Api.IntegrationTests.Common;

// Peticiones de usuarios compartidas por los tests de Auth y Users
public static class UserRequests
{
    public const string Password = "Passw0rd-Tests";

    // Cuerpo de alta con email y documento únicos por llamada: los tests comparten la base de datos
    public static Dictionary<string, object?> NewUserBody(string? role = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var body = new Dictionary<string, object?>
        {
            ["email"] = $"user.{suffix}@dispensario.local",
            ["password"] = Password,
            ["firstName"] = "Laura",
            ["lastName"] = "Vega",
            ["documentType"] = "Passport",
            ["documentNumber"] = suffix,
            ["phone"] = "0991234567",
        };

        if (role is not null)
            body["role"] = role;

        return body;
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, object email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password }, TestContext.Current.CancellationToken);

    public static async Task<Guid> CreateUserAsync(HttpClient adminClient, string role, Dictionary<string, object?>? body = null)
    {
        var response = await adminClient.PostAsJsonAsync("/api/users", body ?? NewUserBody(role), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    public static async Task<Guid> GetCurrentUserIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
